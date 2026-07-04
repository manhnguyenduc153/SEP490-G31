
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Mapster;
using PRN232_be.DTO;
using PRN232_be.DTO.Teacher;
using PRN232_be.Models;
using PRN232_be.Repositories.Interfaces;
using PRN232_be.Services.Interfaces;
using PRN232_be.Helpers;
using PRN232_be.Enums;
using Microsoft.AspNetCore.Identity;

namespace PRN232_be.Services.Implementations
{
    public class TeacherService : ITeacherService
    {
        private readonly ITeacherRepository _repository;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public TeacherService(
            ITeacherRepository repository,
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _repository = repository;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task<ApiResponse<PagingResponse<TeacherDto>>> GetAllAsync(TeacherSearchDto searchDto)
        {
            try
            {
                var query = _repository.FindAll();

                if (!string.IsNullOrWhiteSpace(searchDto.Keyword))
                {
                    query = query.Where(c => c.TextSearch != null && c.TextSearch.Contains(searchDto.Keyword));
                }

                var totalRecords = await query.CountAsync();
                var entities = await query.ApplyPagingAsync(searchDto);

                var dtos = entities.Select(MapToDto).ToList();
                
                // Populate HasAccount in bulk
                var emails = dtos.Select(d => d.Email).Where(email => !string.IsNullOrEmpty(email)).ToList();
                var existingAccountEmails = await _userManager.Users
                    .Where(u => emails.Contains(u.Email))
                    .Select(u => u.Email)
                    .ToListAsync();

                foreach (var dto in dtos)
                {
                    dto.HasAccount = dto.Email != null && existingAccountEmails.Contains(dto.Email);
                }

                var pagingResponse = dtos.ToPagingResponse(totalRecords, searchDto);

                return ApiResponse<PagingResponse<TeacherDto>>.Ok(pagingResponse, "GET_TEACHER_LIST_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<PagingResponse<TeacherDto>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<TeacherDto>> GetByIdAsync(int id)
        {
            try
            {
                var entity = await _repository.GetByIdAsync(id);
                if (entity == null)
                {
                    return ApiResponse<TeacherDto>.Fail("ERR_TEACHER_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                var dto = MapToDto(entity);
                if (!string.IsNullOrEmpty(dto.Email))
                {
                    dto.HasAccount = await _userManager.Users.AnyAsync(u => u.Email == dto.Email);
                }

                return ApiResponse<TeacherDto>.Ok(dto, "GET_TEACHER_DETAIL_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<TeacherDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<TeacherDto>> CreateAsync(TeacherSaveDto dto)
        {
            try
            {
                var validationError = await ValidateAsync(dto, isEdit: false);
                if (validationError != null)
                {
                    return ApiResponse<TeacherDto>.Fail(validationError, StatusCodes.Status400BadRequest);
                }

                var entity = dto.Adapt<Teacher>();
                entity.Id = 0;
                
                // Mặc định Status khi mới tạo là 1
                entity.Status = dto.Status != 0 ? dto.Status : 1;

                await _repository.AddAsync(entity);
                await _repository.SaveChangesAsync();

                var resultDto = MapToDto(entity);
                if (!string.IsNullOrEmpty(resultDto.Email))
                {
                    resultDto.HasAccount = await _userManager.Users.AnyAsync(u => u.Email == resultDto.Email);
                }

                return ApiResponse<TeacherDto>.Created(resultDto, "CREATE_TEACHER_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<TeacherDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<TeacherDto>> EditAsync(TeacherSaveDto dto)
        {
            try
            {
                var validationError = await ValidateAsync(dto, isEdit: true);
                if (validationError != null)
                {
                    return ApiResponse<TeacherDto>.Fail(validationError, StatusCodes.Status400BadRequest);
                }

                var existingEntity = await _repository.GetByIdAsync(dto.Id);
                if (existingEntity == null)
                {
                    return ApiResponse<TeacherDto>.Fail("ERR_TEACHER_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                var oldEmail = existingEntity.Email;
                dto.Adapt(existingEntity);

                await _repository.UpdateAsync(existingEntity);
                await _repository.SaveChangesAsync();

                // If profile is active, make sure user is not locked out
                if (existingEntity.Status == (int)TeacherStatus.Active && !string.IsNullOrEmpty(existingEntity.Email))
                {
                    var user = await _userManager.FindByEmailAsync(existingEntity.Email.Trim());
                    if (user != null && await _userManager.IsLockedOutAsync(user))
                    {
                        await _userManager.SetLockoutEndDateAsync(user, null);
                    }
                }

                // Sync email change to IdentityUser if it exists
                if (!string.Equals(oldEmail, existingEntity.Email, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(oldEmail))
                {
                    var user = await _userManager.FindByEmailAsync(oldEmail.Trim());
                    if (user != null)
                    {
                        var newEmail = existingEntity.Email!.Trim();
                        user.Email = newEmail;
                        user.NormalizedEmail = _userManager.NormalizeEmail(newEmail);

                        var prefix = newEmail.Split('@')[0];
                        var finalUsername = prefix;
                        int suffix = 1;
                        while (await _userManager.FindByNameAsync(finalUsername) != null)
                        {
                            finalUsername = $"{prefix}{suffix++}";
                        }

                        user.UserName = finalUsername;
                        user.NormalizedUserName = _userManager.NormalizeName(finalUsername);

                        await _userManager.UpdateAsync(user);
                    }
                }

                var resultDto = MapToDto(existingEntity);
                if (!string.IsNullOrEmpty(resultDto.Email))
                {
                    resultDto.HasAccount = await _userManager.Users.AnyAsync(u => u.Email == resultDto.Email);
                }

                return ApiResponse<TeacherDto>.Ok(resultDto, "UPDATE_TEACHER_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<TeacherDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<List<TeacherDto>>> ImportAsync(List<TeacherSaveDto> dtos)
        {
            try
            {
                var createdTeachers = new List<Teacher>();
                
                foreach (var dto in dtos)
                {
                    var validationError = await ValidateAsync(dto, isEdit: false);
                    if (validationError != null)
                    {
                        continue; // Skip invalid records
                    }

                    var entity = dto.Adapt<Teacher>();
                    entity.Id = 0;
                    entity.Status = dto.Status != 0 ? dto.Status : 1;

                    await _repository.AddAsync(entity);
                    createdTeachers.Add(entity);
                }

                await _repository.SaveChangesAsync();

                var resultDtos = createdTeachers.Select(MapToDto).ToList();
                // Populate HasAccount
                var emails = resultDtos.Select(d => d.Email).Where(email => !string.IsNullOrEmpty(email)).ToList();
                var existingAccountEmails = await _userManager.Users
                    .Where(u => emails.Contains(u.Email))
                    .Select(u => u.Email)
                    .ToListAsync();

                foreach (var dto in resultDtos)
                {
                    dto.HasAccount = dto.Email != null && existingAccountEmails.Contains(dto.Email);
                }

                return ApiResponse<List<TeacherDto>>.Created(resultDtos, "IMPORT_TEACHERS_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<List<TeacherDto>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<bool>> DeleteAsync(int id)
        {
            try
            {
                var existingEntity = await _repository.GetByIdAsync(id);
                if (existingEntity == null)
                {
                    return ApiResponse<bool>.Fail("ERR_TEACHER_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                // Delete IdentityUser if it exists
                if (!string.IsNullOrEmpty(existingEntity.Email))
                {
                    var user = await _userManager.FindByEmailAsync(existingEntity.Email.Trim());
                    if (user != null)
                    {
                        await _userManager.DeleteAsync(user);
                    }
                }

                await _repository.DeleteAsync(existingEntity);
                await _repository.SaveChangesAsync();

                return ApiResponse<bool>.Ok(true, "DELETE_TEACHER_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<bool>> DeactiveAsync(int id)
        {
            try
            {
                var existingEntity = await _repository.GetByIdAsync(id);
                if (existingEntity == null)
                {
                    return ApiResponse<bool>.Fail("ERR_TEACHER_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                // Lockout IdentityUser if it exists
                if (!string.IsNullOrEmpty(existingEntity.Email))
                {
                    var user = await _userManager.FindByEmailAsync(existingEntity.Email.Trim());
                    if (user != null)
                    {
                        await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
                    }
                }

                await _repository.DeactiveAsync(existingEntity);
                await _repository.SaveChangesAsync();

                return ApiResponse<bool>.Ok(true, "DEACTIVATE_TEACHER_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        private static TeacherDto MapToDto(Teacher entity) => new()
        {
            Id = entity.Id,
            Code = entity.Code ?? string.Empty,
            Name = entity.Name ?? string.Empty,
            Dob = entity.Dob,
            Gender = entity.Gender,
            Email = entity.Email,
            Phone = entity.Phone,
            Address = entity.Address,
            Status = entity.Status,
            Description = entity.Description,
            GradeLevel = entity.GradeLevel,
            GradeLevelName = entity.GradeLevel?.GetStringValue(),
            Avatar = entity.Avatar,
            Certificate = entity.Certificate
        };

        public async Task<ApiResponse<bool>> BulkProvisionAccountsAsync(List<int> teacherIds)
        {
            try
            {
                if (teacherIds == null || !teacherIds.Any())
                {
                    return ApiResponse<bool>.Fail("ERR_NO_TEACHERS_SELECTED", StatusCodes.Status400BadRequest);
                }

                var teachers = await _repository.FindAll()
                    .Where(t => teacherIds.Contains(t.Id) && !t.IsDeleted)
                    .ToListAsync();

                if (!teachers.Any())
                {
                    return ApiResponse<bool>.Fail("ERR_TEACHERS_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                if (!await _roleManager.RoleExistsAsync("Teacher"))
                {
                    await _roleManager.CreateAsync(new IdentityRole("Teacher"));
                }

                var errors = new List<string>();
                int successCount = 0;

                foreach (var teacher in teachers)
                {
                    if (string.IsNullOrWhiteSpace(teacher.Email))
                    {
                        errors.Add($"Giáo viên '{teacher.Name}' (Mã: {teacher.Code}) không có email.");
                        continue;
                    }

                    var emailTrimmed = teacher.Email.Trim();

                    // Check if account already exists for this email
                    var existingUser = await _userManager.FindByEmailAsync(emailTrimmed);
                    if (existingUser != null)
                    {
                        // Account already exists, check role
                        if (!await _userManager.IsInRoleAsync(existingUser, "Teacher"))
                        {
                            await _userManager.AddToRoleAsync(existingUser, "Teacher");
                        }
                        successCount++;
                        continue;
                    }

                    // Extract prefix from email to use as UserName
                    var prefix = emailTrimmed.Split('@')[0];
                    var finalUsername = prefix;
                    int suffix = 1;

                    // Ensure unique username
                    while (await _userManager.FindByNameAsync(finalUsername) != null)
                    {
                        finalUsername = $"{prefix}{suffix++}";
                    }

                    var identityUser = new IdentityUser
                    {
                        UserName = finalUsername,
                        Email = emailTrimmed,
                        PhoneNumber = teacher.Phone?.Trim(),
                        EmailConfirmed = true
                    };

                    var userResult = await _userManager.CreateAsync(identityUser, "123456");
                    if (userResult.Succeeded)
                    {
                        await _userManager.AddToRoleAsync(identityUser, "Teacher");
                        successCount++;
                    }
                    else
                    {
                        var errMsgs = string.Join(", ", userResult.Errors.Select(e => e.Description));
                        errors.Add($"Giáo viên '{teacher.Name}' (Email: {emailTrimmed}) lỗi: {errMsgs}");
                    }
                }

                if (errors.Any())
                {
                    var combinedMessage = string.Join("; ", errors);
                    if (successCount > 0)
                    {
                        return ApiResponse<bool>.Fail($"Cấp tài khoản không hoàn tất: Đã cấp thành công {successCount} tài khoản. Lỗi: {combinedMessage}", StatusCodes.Status400BadRequest);
                    }
                    return ApiResponse<bool>.Fail($"Cấp tài khoản thất bại: {combinedMessage}", StatusCodes.Status400BadRequest);
                }

                return ApiResponse<bool>.Ok(true, "PROVISION_ACCOUNTS_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        private async Task<string?> ValidateAsync(TeacherSaveDto dto, bool isEdit)
        {
            if (string.IsNullOrWhiteSpace(dto.Code))
                return "ERR_CODE_EMPTY";

            if (dto.Code.Length > 50)
                return "ERR_CODE_MAX_LENGTH";

            if (string.IsNullOrWhiteSpace(dto.Name))
                return "ERR_NAME_EMPTY";

            if (dto.Name.Length > 200)
                return "ERR_NAME_MAX_LENGTH";

            if (dto.Email != null && dto.Email.Length > 150)
                return "ERR_EMAIL_MAX_LENGTH";

            if (dto.Phone != null && dto.Phone.Length > 20)
                return "ERR_PHONE_MAX_LENGTH";

            var duplicateCode = await _repository.FindAll()
                .FirstOrDefaultAsync(c => c.Code == dto.Code && (!isEdit || c.Id != dto.Id));

            if (duplicateCode != null)
                return "ERR_CODE_DUPLICATE";

            if (!string.IsNullOrWhiteSpace(dto.Email))
            {
                var duplicateEmail = await _repository.FindAll()
                    .FirstOrDefaultAsync(c => c.Email == dto.Email && (!isEdit || c.Id != dto.Id));

                if (duplicateEmail != null)
                    return "ERR_EMAIL_DUPLICATE";
            }

            return null;
        }
    }
}


