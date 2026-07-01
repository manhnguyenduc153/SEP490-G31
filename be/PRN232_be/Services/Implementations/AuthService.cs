using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using PRN232_be.DTO;
using PRN232_be.DTO.Auth;
using PRN232_be.DTO.Common;
using PRN232_be.Models;
using PRN232_be.Services.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace PRN232_be.Services.Implementations
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _dbContext;

        public AuthService(
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IConfiguration configuration,
            ApplicationDbContext dbContext)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _configuration = configuration;
            _dbContext = dbContext;
        }

        private string GenerateRefreshToken()
        {
            var randomNumber = new byte[64];
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        private async Task<TokenResponseDto> GenerateTokensAsync(IdentityUser user, List<Claim> authClaims)
        {
            var jwtKey = _configuration["Jwt:Key"];
            if (string.IsNullOrEmpty(jwtKey))
            {
                throw new InvalidOperationException("Jwt Key chưa được cấu hình");
            }

            var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            
            var accessTokenMinutes = _configuration.GetValue<int>("Jwt:AccessTokenMinutes", 15);
            var tokenExpirationTimeHour = _configuration.GetValue<int>("Jwt:ExpirationTimeHour", 0);
            
            DateTime tokenExpiry;
            if (accessTokenMinutes > 0)
            {
                tokenExpiry = DateTime.Now.AddMinutes(accessTokenMinutes);
            }
            else
            {
                tokenExpiry = DateTime.Now.AddHours(tokenExpirationTimeHour == 0 ? 3 : tokenExpirationTimeHour);
            }

            var jtiClaim = authClaims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti);
            if (jtiClaim == null)
            {
                jtiClaim = new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString());
                authClaims.Add(jtiClaim);
            }

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                expires: tokenExpiry,
                claims: authClaims,
                signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
            );

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
            var refreshTokenString = GenerateRefreshToken();
            
            var refreshTokenDays = _configuration.GetValue<int>("Jwt:RefreshTokenDays", 7);
            
            var refreshToken = new RefreshToken
            {
                Token = refreshTokenString,
                JwtId = jtiClaim.Value,
                UserId = user.Id,
                AddedDate = DateTime.Now,
                ExpiryDate = DateTime.Now.AddDays(refreshTokenDays),
                IsUsed = false,
                IsRevoked = false
            };

            await _dbContext.RefreshTokens.AddAsync(refreshToken);
            await _dbContext.SaveChangesAsync();

            return new TokenResponseDto
            {
                Token = tokenString,
                Expiration = token.ValidTo,
                RefreshToken = refreshTokenString,
                Username = user.UserName!
            };
        }

        private ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
        {
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = true,
                ValidateIssuer = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!)),
                ValidateLifetime = false,
                ValidIssuer = _configuration["Jwt:Issuer"],
                ValidAudience = _configuration["Jwt:Audience"]
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken securityToken);
            if (securityToken is not JwtSecurityToken jwtSecurityToken || 
                !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                throw new SecurityTokenException("Invalid token");
            }

            return principal;
        }

        public async Task<ApiResponse<TokenResponseDto>> LoginAsync(LoginDto loginDto)
        {
            try
            {
                var user = await _userManager.FindByNameAsync(loginDto.Username);
                if (user == null || !await _userManager.CheckPasswordAsync(user, loginDto.Password))
                {
                    return ApiResponse<TokenResponseDto>.Fail("ERR_INVALID_CREDENTIALS", StatusCodes.Status401Unauthorized);
                }

                if (await _userManager.IsLockedOutAsync(user))
                {
                    return ApiResponse<TokenResponseDto>.Fail("ERR_USER_LOCKED", StatusCodes.Status401Unauthorized);
                }

                var userRoles = await _userManager.GetRolesAsync(user);

                var authClaims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.UserName!),
                    new Claim(ClaimTypes.NameIdentifier, user.Id),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
                };

                foreach (var roleName in userRoles)
                {
                    authClaims.Add(new Claim(ClaimTypes.Role, roleName));
                    var role = await _roleManager.FindByNameAsync(roleName);
                    if (role != null)
                    {
                        var roleClaims = await _roleManager.GetClaimsAsync(role);
                        foreach (var claim in roleClaims)
                        {
                            if (claim.Type.Equals("Permission", StringComparison.OrdinalIgnoreCase))
                            {
                                authClaims.Add(new Claim("Permission", claim.Value));
                            }
                        }
                    }
                }

                var response = await GenerateTokensAsync(user, authClaims);

                return ApiResponse<TokenResponseDto>.Ok(response, "LOGIN_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<TokenResponseDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<TokenResponseDto>> RefreshTokenAsync(RefreshTokenRequestDto requestDto)
        {
            try
            {
                var principal = GetPrincipalFromExpiredToken(requestDto.Token);
                if (principal == null)
                {
                    return ApiResponse<TokenResponseDto>.Fail("ERR_INVALID_TOKEN", StatusCodes.Status400BadRequest);
                }

                var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var jti = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(jti))
                {
                    return ApiResponse<TokenResponseDto>.Fail("ERR_TOKEN_MISSING_INFO", StatusCodes.Status400BadRequest);
                }

                var storedToken = await _dbContext.RefreshTokens
                    .FirstOrDefaultAsync(x => x.Token == requestDto.RefreshToken);

                if (storedToken == null)
                {
                    return ApiResponse<TokenResponseDto>.Fail("ERR_REFRESH_TOKEN_NOT_FOUND", StatusCodes.Status400BadRequest);
                }

                if (storedToken.IsUsed)
                {
                    return ApiResponse<TokenResponseDto>.Fail("ERR_REFRESH_TOKEN_USED", StatusCodes.Status400BadRequest);
                }

                if (storedToken.IsRevoked)
                {
                    return ApiResponse<TokenResponseDto>.Fail("ERR_REFRESH_TOKEN_REVOKED", StatusCodes.Status400BadRequest);
                }

                if (storedToken.JwtId != jti)
                {
                    return ApiResponse<TokenResponseDto>.Fail("ERR_REFRESH_TOKEN_MISMATCH", StatusCodes.Status400BadRequest);
                }

                if (storedToken.ExpiryDate < DateTime.Now)
                {
                    return ApiResponse<TokenResponseDto>.Fail("ERR_REFRESH_TOKEN_EXPIRED", StatusCodes.Status400BadRequest);
                }

                storedToken.IsUsed = true;
                _dbContext.RefreshTokens.Update(storedToken);
                await _dbContext.SaveChangesAsync();

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return ApiResponse<TokenResponseDto>.Fail("ERR_USER_NOT_FOUND", StatusCodes.Status400BadRequest);
                }

                if (await _userManager.IsLockedOutAsync(user))
                {
                    return ApiResponse<TokenResponseDto>.Fail("ERR_USER_LOCKED", StatusCodes.Status400BadRequest);
                }

                var userRoles = await _userManager.GetRolesAsync(user);
                var authClaims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.UserName!),
                    new Claim(ClaimTypes.NameIdentifier, user.Id),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
                };

                foreach (var roleName in userRoles)
                {
                    authClaims.Add(new Claim(ClaimTypes.Role, roleName));
                    var role = await _roleManager.FindByNameAsync(roleName);
                    if (role != null)
                    {
                        var roleClaims = await _roleManager.GetClaimsAsync(role);
                        foreach (var claim in roleClaims)
                        {
                            if (claim.Type.Equals("Permission", StringComparison.OrdinalIgnoreCase))
                            {
                                authClaims.Add(new Claim("Permission", claim.Value));
                            }
                        }
                    }
                }

                var response = await GenerateTokensAsync(user, authClaims);
                return ApiResponse<TokenResponseDto>.Ok(response, "REFRESH_TOKEN_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<TokenResponseDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<bool>> LogoutAsync(LogoutRequestDto requestDto)
        {
            try
            {
                var storedToken = await _dbContext.RefreshTokens
                    .FirstOrDefaultAsync(x => x.Token == requestDto.RefreshToken);

                if (storedToken == null)
                {
                    return ApiResponse<bool>.Fail("ERR_REFRESH_TOKEN_INVALID_OR_NOT_FOUND", StatusCodes.Status400BadRequest);
                }

                storedToken.IsRevoked = true;
                _dbContext.RefreshTokens.Update(storedToken);
                await _dbContext.SaveChangesAsync();

                return ApiResponse<bool>.Ok(true, "LOGOUT_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<bool>> RegisterAsync(RegisterDto registerDto)
        {
            try
            {
                var userExists = await _userManager.FindByNameAsync(registerDto.Username);
                if (userExists != null)
                {
                    return ApiResponse<bool>.Fail("ERR_USER_EXISTS", StatusCodes.Status400BadRequest);
                }

                IdentityUser user = new()
                {
                    Email = registerDto.Email,
                    SecurityStamp = Guid.NewGuid().ToString(),
                    UserName = registerDto.Username
                };

                var result = await _userManager.CreateAsync(user, registerDto.Password);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    return ApiResponse<bool>.Fail("ERR_REGISTER_FAILED", StatusCodes.Status400BadRequest);
                }

                return ApiResponse<bool>.Ok(true, "REGISTER_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<bool>> CreateRoleAsync(CreateRoleDto createRoleDto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(createRoleDto.RoleName))
                {
                    return ApiResponse<bool>.Fail("ERR_ROLE_NAME_EMPTY", StatusCodes.Status400BadRequest);
                }

                var roleExists = await _roleManager.RoleExistsAsync(createRoleDto.RoleName);
                if (roleExists)
                {
                    return ApiResponse<bool>.Fail("ERR_ROLE_DUPLICATE", StatusCodes.Status400BadRequest);
                }

                var result = await _roleManager.CreateAsync(new IdentityRole(createRoleDto.RoleName));
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    return ApiResponse<bool>.Fail("ERR_CREATE_ROLE_FAILED", StatusCodes.Status400BadRequest);
                }

                return ApiResponse<bool>.Ok(true, "CREATE_ROLE_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<bool>> AssignRolePermissionsAsync(AssignRolePermissionsDto assignRolePermissionsDto)
        {
            try
            {
                var role = await _roleManager.FindByNameAsync(assignRolePermissionsDto.RoleName);
                if (role == null)
                {
                    return ApiResponse<bool>.Fail("ERR_ROLE_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                // 1. Lấy tất cả các Claim dạng Permission của Role này trực tiếp từ DbContext
                var existingClaims = await _dbContext.RoleClaims
                    .Where(rc => rc.RoleId == role.Id && rc.ClaimType == "Permission")
                    .ToListAsync();

                var newPermissions = assignRolePermissionsDto.Permissions ?? new List<string>();

                // 2. Lọc ra các claim cần xóa (có trong DB nhưng không có trong danh sách mới gửi lên)
                var claimsToDelete = existingClaims
                    .Where(c => !newPermissions.Contains(c.ClaimValue!))
                    .ToList();

                // 3. Lọc ra các permission cần thêm mới (có trong danh sách mới nhưng chưa có trong DB)
                var existingPermissionValues = existingClaims.Select(c => c.ClaimValue!).ToList();
                var permissionsToAdd = newPermissions
                    .Except(existingPermissionValues)
                    .Select(p => new IdentityRoleClaim<string>
                    {
                        RoleId = role.Id,
                        ClaimType = "Permission",
                        ClaimValue = p
                    })
                    .ToList();

                // 4. Thực hiện xóa/thêm hàng loạt bằng DbContext để tối ưu hiệu năng
                if (claimsToDelete.Any())
                {
                    _dbContext.RoleClaims.RemoveRange(claimsToDelete);
                }

                if (permissionsToAdd.Any())
                {
                    await _dbContext.RoleClaims.AddRangeAsync(permissionsToAdd);
                }

                // Lưu thay đổi chỉ với 1 lần kết nối Database duy nhất
                await _dbContext.SaveChangesAsync();

                return ApiResponse<bool>.Ok(true, "ASSIGN_PERMISSIONS_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<bool>> AssignUserRoleAsync(string username, string roleName)
        {
            try
            {
                var user = await _userManager.FindByNameAsync(username);
                if (user == null)
                {
                    return ApiResponse<bool>.Fail("ERR_USER_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                var roleExists = await _roleManager.RoleExistsAsync(roleName);
                if (!roleExists)
                {
                    return ApiResponse<bool>.Fail("ERR_ROLE_NOT_FOUND", StatusCodes.Status400BadRequest);
                }

                if (await _userManager.IsInRoleAsync(user, roleName))
                {
                    return ApiResponse<bool>.Fail("ERR_USER_ALREADY_HAS_ROLE", StatusCodes.Status400BadRequest);
                }

                var result = await _userManager.AddToRoleAsync(user, roleName);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    return ApiResponse<bool>.Fail("ERR_ASSIGN_ROLE_FAILED", StatusCodes.Status400BadRequest);
                }

                return ApiResponse<bool>.Ok(true, "ASSIGN_ROLE_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<List<string>>> GetAllRolesAsync()
        {
            try
            {
                var roles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync();
                return ApiResponse<List<string>>.Ok(roles, "GET_ROLE_LIST_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<List<string>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<PagingResponse<RoleDto>>> GetAllRoleAsync(RoleSearchDto searchDto)
        {
            try
            {
                var query = _dbContext.Roles.AsQueryable();

                if (!string.IsNullOrWhiteSpace(searchDto.Keyword))
                {
                    query = query.Where(r => r.Name != null && r.Name.Contains(searchDto.Keyword));
                }

                var totalRecords = await query.CountAsync();

                var roles = await query
                    .Skip((searchDto.PageIndex - 1) * searchDto.PageSize)
                    .Take(searchDto.PageSize)
                    .ToListAsync();

                var roleIds = roles.Select(r => r.Id).ToList();
                var roleClaims = await _dbContext.RoleClaims
                    .Where(rc => roleIds.Contains(rc.RoleId) && rc.ClaimType == "Permission")
                    .ToListAsync();

                var roleDtos = roles.Select(role => new RoleDto
                {
                    Id = role.Id,
                    Name = role.Name ?? string.Empty,
                    Description = $"Vai trò {role.Name}",
                    Status = "Active",
                    CreatedAt = new DateTime(2026, 5, 19),
                    Permissions = roleClaims
                        .Where(rc => rc.RoleId == role.Id)
                        .Select(rc => rc.ClaimValue ?? string.Empty)
                        .ToList()
                }).ToList();

                var pagingResponse = new PagingResponse<RoleDto>
                {
                    PageIndex = searchDto.PageIndex,
                    PageSize = searchDto.PageSize,
                    TotalRecords = totalRecords,
                    Items = roleDtos
                };

                return ApiResponse<PagingResponse<RoleDto>>.Ok(pagingResponse, "GET_ROLE_LIST_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<PagingResponse<RoleDto>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public Task<ApiResponse<List<string>>> GetAllPermissionsAsync()
        {
            try
            {
                var permissions = Permissions.GetAllPermissions();
                return Task.FromResult(ApiResponse<List<string>>.Ok(permissions, "GET_SYSTEM_PERMISSIONS_SUCCESS"));
            }
            catch (Exception ex)
            {
                return Task.FromResult(ApiResponse<List<string>>.Fail(ex.Message, StatusCodes.Status500InternalServerError));
            }
        }

        public async Task<ApiResponse<List<string>>> GetUserRolesAsync(string username)
        {
            try
            {
                var user = await _userManager.FindByNameAsync(username);
                if (user == null)
                {
                    return ApiResponse<List<string>>.Fail("ERR_USER_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                var roles = (await _userManager.GetRolesAsync(user)).ToList();
                return ApiResponse<List<string>>.Ok(roles, "GET_USER_ROLES_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<List<string>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<List<string>>> GetUserPermissionsAsync(string username)
        {
            try
            {
                var user = await _userManager.FindByNameAsync(username);
                if (user == null)
                {
                    return ApiResponse<List<string>>.Fail("ERR_USER_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                var roles = await _userManager.GetRolesAsync(user);
                var permissions = new List<string>();

                foreach (var roleName in roles)
                {
                    var role = await _roleManager.FindByNameAsync(roleName);
                    if (role != null)
                    {
                        var roleClaims = await _roleManager.GetClaimsAsync(role);
                        foreach (var claim in roleClaims)
                        {
                            if (claim.Type.Equals("Permission", StringComparison.OrdinalIgnoreCase))
                            {
                                permissions.Add(claim.Value);
                            }
                        }
                    }
                }

                permissions = permissions.Distinct().ToList();

                return ApiResponse<List<string>>.Ok(permissions, "GET_USER_PERMISSIONS_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<List<string>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<List<string>>> GetRolePermissionsAsync(string roleName)
        {
            try
            {
                var role = await _roleManager.FindByNameAsync(roleName);
                if (role == null)
                {
                    return ApiResponse<List<string>>.Fail("ERR_ROLE_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                var permissions = await _dbContext.RoleClaims
                    .Where(rc => rc.RoleId == role.Id && rc.ClaimType == "Permission")
                    .Select(rc => rc.ClaimValue!)
                    .ToListAsync();

                return ApiResponse<List<string>>.Ok(permissions, "GET_ROLE_PERMISSIONS_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<List<string>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }
    }
}
