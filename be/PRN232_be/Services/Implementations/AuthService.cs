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

        public async Task<ApiResponse<TokenResponseDto>> LoginAsync(LoginDto loginDto)
        {
            try
            {
                var user = await _userManager.FindByNameAsync(loginDto.Username);
                if (user == null || !await _userManager.CheckPasswordAsync(user, loginDto.Password))
                {
                    return ApiResponse<TokenResponseDto>.Fail("Tên đăng nhập hoặc mật khẩu không đúng", StatusCodes.Status401Unauthorized);
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

                var jwtKey = _configuration["Jwt:Key"];
                if (string.IsNullOrEmpty(jwtKey))
                {
                    return ApiResponse<TokenResponseDto>.Fail("Jwt Key chưa được cấu hình", StatusCodes.Status500InternalServerError);
                }

                var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
                var tokenExpirationTimeHour = _configuration.GetValue<int>("Jwt:ExpirationTimeHour", 3);

                var token = new JwtSecurityToken(
                    issuer: _configuration["Jwt:Issuer"],
                    audience: _configuration["Jwt:Audience"],
                    expires: DateTime.Now.AddHours(tokenExpirationTimeHour == 0 ? 3 : tokenExpirationTimeHour),
                    claims: authClaims,
                    signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
                );

                var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

                var response = new TokenResponseDto
                {
                    Token = tokenString,
                    Expiration = token.ValidTo,
                    Username = user.UserName!
                };

                return ApiResponse<TokenResponseDto>.Ok(response, "Đăng nhập thành công");
            }
            catch (Exception ex)
            {
                return ApiResponse<TokenResponseDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<bool>> RegisterAsync(RegisterDto registerDto)
        {
            try
            {
                var userExists = await _userManager.FindByNameAsync(registerDto.Username);
                if (userExists != null)
                {
                    return ApiResponse<bool>.Fail("Người dùng đã tồn tại", StatusCodes.Status400BadRequest);
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
                    return ApiResponse<bool>.Fail($"Tạo tài khoản thất bại: {errors}", StatusCodes.Status400BadRequest);
                }

                return ApiResponse<bool>.Ok(true, "Tạo tài khoản thành công");
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
                    return ApiResponse<bool>.Fail("Tên vai trò không được để trống", StatusCodes.Status400BadRequest);
                }

                var roleExists = await _roleManager.RoleExistsAsync(createRoleDto.RoleName);
                if (roleExists)
                {
                    return ApiResponse<bool>.Fail("Vai trò đã tồn tại", StatusCodes.Status400BadRequest);
                }

                var result = await _roleManager.CreateAsync(new IdentityRole(createRoleDto.RoleName));
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    return ApiResponse<bool>.Fail($"Tạo vai trò thất bại: {errors}", StatusCodes.Status400BadRequest);
                }

                return ApiResponse<bool>.Ok(true, "Tạo vai trò thành công");
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
                    return ApiResponse<bool>.Fail("Vai trò không tồn tại", StatusCodes.Status404NotFound);
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

                return ApiResponse<bool>.Ok(true, "Gán quyền thành công");
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
                    return ApiResponse<bool>.Fail("Người dùng không tồn tại", StatusCodes.Status404NotFound);
                }

                var roleExists = await _roleManager.RoleExistsAsync(roleName);
                if (!roleExists)
                {
                    return ApiResponse<bool>.Fail("Vai trò không tồn tại", StatusCodes.Status400BadRequest);
                }

                if (await _userManager.IsInRoleAsync(user, roleName))
                {
                    return ApiResponse<bool>.Fail("Người dùng đã có vai trò này", StatusCodes.Status400BadRequest);
                }

                var result = await _userManager.AddToRoleAsync(user, roleName);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    return ApiResponse<bool>.Fail($"Gán vai trò thất bại: {errors}", StatusCodes.Status400BadRequest);
                }

                return ApiResponse<bool>.Ok(true, "Gán vai trò thành công");
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
                return ApiResponse<List<string>>.Ok(roles, "Lấy danh sách vai trò thành công");
            }
            catch (Exception ex)
            {
                return ApiResponse<List<string>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public Task<ApiResponse<List<string>>> GetAllPermissionsAsync()
        {
            try
            {
                var permissions = DmsPermissions.GetAllPermissions();
                return Task.FromResult(ApiResponse<List<string>>.Ok(permissions, "Lấy danh sách quyền hệ thống thành công"));
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
                    return ApiResponse<List<string>>.Fail("Người dùng không tồn tại", StatusCodes.Status404NotFound);
                }

                var roles = (await _userManager.GetRolesAsync(user)).ToList();
                return ApiResponse<List<string>>.Ok(roles, "Lấy vai trò của người dùng thành công");
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
                    return ApiResponse<List<string>>.Fail("Người dùng không tồn tại", StatusCodes.Status404NotFound);
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

                return ApiResponse<List<string>>.Ok(permissions, "Lấy danh sách quyền của người dùng thành công");
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
                    return ApiResponse<List<string>>.Fail("Vai trò không tồn tại", StatusCodes.Status404NotFound);
                }

                var permissions = await _dbContext.RoleClaims
                    .Where(rc => rc.RoleId == role.Id && rc.ClaimType == "Permission")
                    .Select(rc => rc.ClaimValue!)
                    .ToListAsync();

                return ApiResponse<List<string>>.Ok(permissions, "Lấy quyền của vai trò thành công");
            }
            catch (Exception ex)
            {
                return ApiResponse<List<string>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }
    }
}
