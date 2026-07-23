using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using sep490_be.DTO;
using sep490_be.DTO.User;
using sep490_be.Models;
using sep490_be.Services.Interfaces;
using sep490_be.Helpers;

namespace sep490_be.Services.Implementations
{
    public class UserService : IUserService
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _dbContext;

        public UserService(
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext dbContext)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _dbContext = dbContext;
        }

        public async Task<ApiResponse<PagingResponse<UserDto>>> GetAllAsync(UserSearchDto searchDto)
        {
            try
            {
                var query = _userManager.Users;

                if (!string.IsNullOrWhiteSpace(searchDto.Keyword))
                {
                    query = query.Where(u => u.UserName!.Contains(searchDto.Keyword) || 
                                             u.Email!.Contains(searchDto.Keyword) || 
                                             (u.PhoneNumber != null && u.PhoneNumber.Contains(searchDto.Keyword)));
                }

                if (!string.IsNullOrWhiteSpace(searchDto.RoleName))
                {
                    var role = await _roleManager.FindByNameAsync(searchDto.RoleName);
                    if (role != null)
                    {
                        var userIdsInRole = await _dbContext.UserRoles
                            .Where(ur => ur.RoleId == role.Id)
                            .Select(ur => ur.UserId)
                            .ToListAsync();
                        query = query.Where(u => userIdsInRole.Contains(u.Id));
                    }
                    else
                    {
                        return ApiResponse<PagingResponse<UserDto>>.Ok(new PagingResponse<UserDto>
                        {
                            PageIndex = searchDto.PageIndex,
                            PageSize = searchDto.PageSize,
                            TotalRecords = 0,
                            Items = new List<UserDto>()
                        }, "GET_USER_LIST_SUCCESS");
                    }
                }

                var totalRecords = await query.CountAsync();
                
                var users = await query
                    .Skip((searchDto.PageIndex - 1) * searchDto.PageSize)
                    .Take(searchDto.PageSize)
                    .ToListAsync();

                var dtos = new List<UserDto>();
                foreach (var user in users)
                {
                    var roles = await _userManager.GetRolesAsync(user);
                    dtos.Add(new UserDto
                    {
                        Id = user.Id,
                        Username = user.UserName ?? string.Empty,
                        Email = user.Email ?? string.Empty,
                        Phone = user.PhoneNumber ?? string.Empty,
                        Roles = roles.ToList(),
                        Status = (user.LockoutEnd != null && user.LockoutEnd > DateTimeOffset.UtcNow) ? 0 : 1
                    });
                }

                var pagingResponse = dtos.ToPagingResponse(totalRecords, searchDto);

                return ApiResponse<PagingResponse<UserDto>>.Ok(pagingResponse, "GET_USER_LIST_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<PagingResponse<UserDto>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<UserDto>> GetByIdAsync(string id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    return ApiResponse<UserDto>.Fail("ERR_USER_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                var roles = await _userManager.GetRolesAsync(user);
                var dto = new UserDto
                {
                    Id = user.Id,
                    Username = user.UserName ?? string.Empty,
                    Email = user.Email ?? string.Empty,
                    Phone = user.PhoneNumber ?? string.Empty,
                    Roles = roles.ToList(),
                    Status = (user.LockoutEnd != null && user.LockoutEnd > DateTimeOffset.UtcNow) ? 0 : 1
                };

                return ApiResponse<UserDto>.Ok(dto, "GET_USER_DETAIL_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<UserDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<UserDto>> CreateAsync(UserCreateDto dto)
        {
            try
            {
                var validationError = await ValidateCreateAsync(dto);
                if (validationError != null)
                {
                    return ApiResponse<UserDto>.Fail(validationError, StatusCodes.Status400BadRequest);
                }

                var roleExists = await _roleManager.RoleExistsAsync(dto.RoleName);
                if (!roleExists)
                {
                    return ApiResponse<UserDto>.Fail("ERR_ROLE_NOT_FOUND", StatusCodes.Status400BadRequest);
                }

                var user = new IdentityUser
                {
                    UserName = dto.Username,
                    Email = dto.Email,
                    PhoneNumber = dto.Phone,
                    SecurityStamp = Guid.NewGuid().ToString(),
                    LockoutEnabled = true
                };

                var result = await _userManager.CreateAsync(user, "123456");
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    return ApiResponse<UserDto>.Fail($"ERR_CREATE_USER_FAILED: {errors}", StatusCodes.Status400BadRequest);
                }

                var roleResult = await _userManager.AddToRoleAsync(user, dto.RoleName);
                if (!roleResult.Succeeded)
                {
                    var errors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
                    return ApiResponse<UserDto>.Fail($"ERR_ASSIGN_ROLE_FAILED: {errors}", StatusCodes.Status400BadRequest);
                }

                var userDto = new UserDto
                {
                    Id = user.Id,
                    Username = user.UserName,
                    Email = user.Email,
                    Phone = user.PhoneNumber ?? string.Empty,
                    Roles = new List<string> { dto.RoleName },
                    Status = 1
                };

                return ApiResponse<UserDto>.Created(userDto, "CREATE_USER_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<UserDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<UserDto>> EditAsync(UserUpdateDto dto)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(dto.Id);
                if (user == null)
                {
                    return ApiResponse<UserDto>.Fail("ERR_USER_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                var validationError = await ValidateUpdateAsync(dto);
                if (validationError != null)
                {
                    return ApiResponse<UserDto>.Fail(validationError, StatusCodes.Status400BadRequest);
                }

                if (!string.IsNullOrWhiteSpace(dto.RoleName))
                {
                    var roleExists = await _roleManager.RoleExistsAsync(dto.RoleName);
                    if (!roleExists)
                    {
                        return ApiResponse<UserDto>.Fail("ERR_ROLE_NOT_FOUND", StatusCodes.Status400BadRequest);
                    }
                }

                user.Email = dto.Email;
                user.PhoneNumber = dto.Phone;

                var result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    return ApiResponse<UserDto>.Fail($"ERR_UPDATE_USER_FAILED: {errors}", StatusCodes.Status400BadRequest);
                }

                if (!string.IsNullOrWhiteSpace(dto.RoleName))
                {
                    var currentRoles = await _userManager.GetRolesAsync(user);
                    if (!currentRoles.Contains(dto.RoleName))
                    {
                        if (currentRoles.Any())
                        {
                            var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
                            if (!removeResult.Succeeded)
                            {
                                var errors = string.Join(", ", removeResult.Errors.Select(e => e.Description));
                                return ApiResponse<UserDto>.Fail($"ERR_REMOVE_ROLES_FAILED: {errors}", StatusCodes.Status400BadRequest);
                            }
                        }

                        var addRoleResult = await _userManager.AddToRoleAsync(user, dto.RoleName);
                        if (!addRoleResult.Succeeded)
                        {
                            var errors = string.Join(", ", addRoleResult.Errors.Select(e => e.Description));
                            return ApiResponse<UserDto>.Fail($"ERR_ASSIGN_ROLE_FAILED: {errors}", StatusCodes.Status400BadRequest);
                        }
                    }
                }

                var finalRoles = await _userManager.GetRolesAsync(user);
                var userDto = new UserDto
                {
                    Id = user.Id,
                    Username = user.UserName!,
                    Email = user.Email,
                    Phone = user.PhoneNumber ?? string.Empty,
                    Roles = finalRoles.ToList(),
                    Status = (user.LockoutEnd != null && user.LockoutEnd > DateTimeOffset.UtcNow) ? 0 : 1
                };

                return ApiResponse<UserDto>.Ok(userDto, "UPDATE_USER_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<UserDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<bool>> DeleteAsync(string id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    return ApiResponse<bool>.Fail("ERR_USER_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                var result = await _userManager.DeleteAsync(user);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    return ApiResponse<bool>.Fail($"ERR_DELETE_USER_FAILED: {errors}", StatusCodes.Status400BadRequest);
                }

                return ApiResponse<bool>.Ok(true, "DELETE_USER_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        private async Task<string?> ValidateCreateAsync(UserCreateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Username))
                return "ERR_USERNAME_EMPTY";

            if (dto.Username.Length > 256)
                return "ERR_USERNAME_MAX_LENGTH";

            if (string.IsNullOrWhiteSpace(dto.Email))
                return "ERR_EMAIL_EMPTY";

            if (dto.RoleName.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                return "ERR_CANNOT_ASSIGN_ADMIN_ROLE";

            var existingUserByUsername = await _userManager.FindByNameAsync(dto.Username);
            if (existingUserByUsername != null)
                return "ERR_USERNAME_DUPLICATE";

            var existingUserByEmail = await _userManager.FindByEmailAsync(dto.Email);
            if (existingUserByEmail != null)
                return "ERR_EMAIL_DUPLICATE";

            return null;
        }

        private async Task<string?> ValidateUpdateAsync(UserUpdateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email))
                return "ERR_EMAIL_EMPTY";

            if (!string.IsNullOrWhiteSpace(dto.RoleName) && dto.RoleName.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                var user = await _userManager.FindByIdAsync(dto.Id);
                if (user != null)
                {
                    var userRoles = await _userManager.GetRolesAsync(user);
                    if (!userRoles.Any(r => r.Equals("Admin", StringComparison.OrdinalIgnoreCase)))
                    {
                        return "ERR_CANNOT_ASSIGN_ADMIN_ROLE";
                    }
                }
            }

            var existingUserByEmail = await _userManager.FindByEmailAsync(dto.Email);
            if (existingUserByEmail != null && existingUserByEmail.Id != dto.Id)
                return "ERR_EMAIL_DUPLICATE";

            return null;
        }

        public async Task<ApiResponse<bool>> DeactiveAsync(string id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    return ApiResponse<bool>.Fail("ERR_USER_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                var isLocked = await _userManager.IsLockedOutAsync(user);
                IdentityResult result;
                if (isLocked)
                {
                    result = await _userManager.SetLockoutEndDateAsync(user, null);
                }
                else
                {
                    await _userManager.SetLockoutEnabledAsync(user, true);
                    result = await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
                }

                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    return ApiResponse<bool>.Fail($"ERR_DEACTIVE_USER_FAILED: {errors}", StatusCodes.Status400BadRequest);
                }

                return ApiResponse<bool>.Ok(true, "DEACTIVATE_USER_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }
    }
}

