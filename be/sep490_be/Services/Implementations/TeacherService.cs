
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Mapster;
using sep490_be.DTO;
using sep490_be.DTO.Teacher;
using sep490_be.Models;
using sep490_be.Repositories.Interfaces;
using sep490_be.Services.Interfaces;
using sep490_be.Helpers;
using sep490_be.Enums;
using Microsoft.AspNetCore.Identity;
using System.Text.Json;
using System.Net.Mail;
using System.Text.RegularExpressions;

namespace sep490_be.Services.Implementations
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

        public async Task<ApiResponse<PagingResponse<TeacherDto>>> GetAllAsync(TeacherSearchDto searchDto, string? username, bool hasViewPermission)
        {
            try
            {
                if (!hasViewPermission)
                {
                    if (string.IsNullOrEmpty(username))
                    {
                        return ApiResponse<PagingResponse<TeacherDto>>.Fail("FORBIDDEN", StatusCodes.Status403Forbidden);
                    }

                    var user = await _userManager.FindByNameAsync(username);
                    if (user == null)
                    {
                        return ApiResponse<PagingResponse<TeacherDto>>.Fail("FORBIDDEN", StatusCodes.Status403Forbidden);
                    }

                    var isSearchingSelf = !string.IsNullOrEmpty(searchDto.Keyword) && 
                        (string.Equals(searchDto.Keyword, user.UserName, StringComparison.OrdinalIgnoreCase) || 
                         string.Equals(searchDto.Keyword, user.Email, StringComparison.OrdinalIgnoreCase));

                    if (!isSearchingSelf)
                    {
                        if (string.IsNullOrEmpty(searchDto.Keyword))
                        {
                            searchDto.Keyword = user.Email;
                        }
                        else
                        {
                            return ApiResponse<PagingResponse<TeacherDto>>.Fail("FORBIDDEN", StatusCodes.Status403Forbidden);
                        }
                    }
                    else
                    {
                        searchDto.Keyword = user.Email;
                    }
                }

                var query = _repository.FindAll();

                if (!string.IsNullOrWhiteSpace(searchDto.Keyword))
                {
                    var keyword = searchDto.Keyword.Trim();
                    query = query.Where(c => c.TextSearch != null && c.TextSearch.Contains(keyword));
                }

                if (searchDto.TeacherStatus.HasValue)
                {
                    query = query.Where(c => c.Status == searchDto.TeacherStatus.Value);
                }

                if (searchDto.Gender.HasValue)
                {
                    query = query.Where(c => c.Gender == searchDto.Gender.Value);
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
            catch (Exception)
            {
                return ApiResponse<PagingResponse<TeacherDto>>.Fail("ERR_INTERNAL_SERVER_ERROR", StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<TeacherDto>> GetByIdAsync(int id, string? username, bool hasViewPermission)
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

                if (!hasViewPermission)
                {
                    var isViewingSelf = false;
                    if (!string.IsNullOrEmpty(username))
                    {
                        var user = await _userManager.FindByNameAsync(username);
                        if (user != null && string.Equals(dto.Email, user.Email, StringComparison.OrdinalIgnoreCase))
                        {
                            isViewingSelf = true;
                        }
                    }
                    if (!isViewingSelf)
                    {
                        return ApiResponse<TeacherDto>.Fail("FORBIDDEN", StatusCodes.Status403Forbidden);
                    }
                }

                return ApiResponse<TeacherDto>.Ok(dto, "GET_TEACHER_DETAIL_SUCCESS");
            }
            catch (Exception)
            {
                return ApiResponse<TeacherDto>.Fail("ERR_INTERNAL_SERVER_ERROR", StatusCodes.Status500InternalServerError);
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
                entity.Certificate = SerializeCertificates(dto.Certificates);
                
                // Mặc định Status khi mới tạo là 1
                entity.Status = dto.Status;
                entity.TextSearch = dto.TextSearch;

                await _repository.AddAsync(entity);
                await _repository.SaveChangesAsync();

                var resultDto = MapToDto(entity);
                if (!string.IsNullOrEmpty(resultDto.Email))
                {
                    resultDto.HasAccount = await _userManager.Users.AnyAsync(u => u.Email == resultDto.Email);
                }

                return ApiResponse<TeacherDto>.Created(resultDto, "CREATE_TEACHER_SUCCESS");
            }
            catch (Exception)
            {
                return ApiResponse<TeacherDto>.Fail("ERR_INTERNAL_SERVER_ERROR", StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<TeacherDto>> EditAsync(TeacherSaveDto dto, string? username, bool hasEditPermission)
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

                if (!hasEditPermission)
                {
                    var isEditingSelf = false;
                    if (!string.IsNullOrEmpty(username))
                    {
                        var user = await _userManager.FindByNameAsync(username);
                        if (user != null && string.Equals(existingEntity.Email, user.Email, StringComparison.OrdinalIgnoreCase))
                        {
                            isEditingSelf = true;
                        }
                    }
                    if (!isEditingSelf)
                    {
                        return ApiResponse<TeacherDto>.Fail("FORBIDDEN", StatusCodes.Status403Forbidden);
                    }
                }

                var oldEmail = existingEntity.Email;
                IdentityUser? identityUser = null;
                if (!string.IsNullOrWhiteSpace(oldEmail))
                {
                    identityUser = await _userManager.FindByEmailAsync(oldEmail.Trim());
                    if (identityUser != null && string.IsNullOrWhiteSpace(dto.Email))
                    {
                        return ApiResponse<TeacherDto>.Fail("ERR_EMAIL_REQUIRED_FOR_ACCOUNT", StatusCodes.Status400BadRequest);
                    }
                }

                dto.Adapt(existingEntity);
                existingEntity.Certificate = SerializeCertificates(dto.Certificates);
                existingEntity.TextSearch = dto.TextSearch;

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
                    if (identityUser != null)
                    {
                        var newEmail = existingEntity.Email!.Trim();
                        identityUser.Email = newEmail;
                        identityUser.NormalizedEmail = _userManager.NormalizeEmail(newEmail);

                        var prefix = newEmail.Split('@')[0];
                        var finalUsername = prefix;
                        int suffix = 1;
                        IdentityUser? usernameOwner;
                        while ((usernameOwner = await _userManager.FindByNameAsync(finalUsername)) != null
                            && usernameOwner.Id != identityUser.Id)
                        {
                            finalUsername = $"{prefix}{suffix++}";
                        }

                        identityUser.UserName = finalUsername;
                        identityUser.NormalizedUserName = _userManager.NormalizeName(finalUsername);

                        var updateResult = await _userManager.UpdateAsync(identityUser);
                        if (!updateResult.Succeeded)
                        {
                            return ApiResponse<TeacherDto>.Fail("ERR_TEACHER_ACCOUNT_UPDATE_FAILED", StatusCodes.Status400BadRequest);
                        }
                    }
                }

                var resultDto = MapToDto(existingEntity);
                if (!string.IsNullOrEmpty(resultDto.Email))
                {
                    resultDto.HasAccount = await _userManager.Users.AnyAsync(u => u.Email == resultDto.Email);
                }

                return ApiResponse<TeacherDto>.Ok(resultDto, "UPDATE_TEACHER_SUCCESS");
            }
            catch (Exception)
            {
                return ApiResponse<TeacherDto>.Fail("ERR_INTERNAL_SERVER_ERROR", StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<List<TeacherDto>>> ImportAsync(List<TeacherSaveDto> dtos)
        {
            try
            {
                if (dtos == null || dtos.Count == 0)
                {
                    return ApiResponse<List<TeacherDto>>.Fail("ERR_TEACHER_IMPORT_EMPTY", StatusCodes.Status400BadRequest);
                }

                // Pre-load all existing active/inactive codes, emails, phones to memory for fast checking
                var existingTeachers = await _repository.FindAll()
                    .Select(t => new { t.Code, t.Email, t.Phone })
                    .ToListAsync();

                var existingCodes = new HashSet<string>(
                    existingTeachers.Where(t => !string.IsNullOrEmpty(t.Code)).Select(t => t.Code.Trim()), 
                    StringComparer.OrdinalIgnoreCase);

                var existingEmails = new HashSet<string>(
                    existingTeachers.Where(t => !string.IsNullOrEmpty(t.Email)).Select(t => t.Email!.Trim()), 
                    StringComparer.OrdinalIgnoreCase);

                var existingPhones = new HashSet<string>(
                    existingTeachers.Where(t => !string.IsNullOrEmpty(t.Phone)).Select(t => t.Phone!.Trim()), 
                    StringComparer.OrdinalIgnoreCase);

                var createdTeachers = new List<Teacher>();
                var batchCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var batchEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var batchPhones = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var dto in dtos)
                {
                    var cleanCode = dto.Code?.Trim() ?? string.Empty;
                    var cleanEmail = dto.Email?.Trim();
                    var cleanPhone = dto.Phone?.Trim();

                    // Check if record exists in DB or in current batch (by Code, Email, or Phone) -> Skip
                    if (!string.IsNullOrEmpty(cleanCode) && (existingCodes.Contains(cleanCode) || batchCodes.Contains(cleanCode)))
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(cleanEmail) && (existingEmails.Contains(cleanEmail) || batchEmails.Contains(cleanEmail)))
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(cleanPhone) && (existingPhones.Contains(cleanPhone) || batchPhones.Contains(cleanPhone)))
                    {
                        continue;
                    }

                    // Validate other field constraints (format, length, etc.)
                    var validationError = await ValidateAsync(dto, isEdit: false);
                    if (validationError != null)
                    {
                        // If validation error is duplicate code/email, skip
                        if (validationError == "ERR_CODE_DUPLICATE" || validationError == "ERR_EMAIL_DUPLICATE")
                        {
                            continue;
                        }
                        return ApiResponse<List<TeacherDto>>.Fail(validationError, StatusCodes.Status400BadRequest);
                    }

                    if (!string.IsNullOrEmpty(cleanCode)) batchCodes.Add(cleanCode);
                    if (!string.IsNullOrEmpty(cleanEmail)) batchEmails.Add(cleanEmail);
                    if (!string.IsNullOrEmpty(cleanPhone)) batchPhones.Add(cleanPhone);

                    var entity = dto.Adapt<Teacher>();
                    entity.Id = 0;
                    entity.Certificate = SerializeCertificates(dto.Certificates);
                    entity.Status = dto.Status;
                    entity.TextSearch = dto.TextSearch;

                    await _repository.AddAsync(entity);
                    createdTeachers.Add(entity);
                }

                if (createdTeachers.Any())
                {
                    await _repository.SaveChangesAsync();
                }

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

                return ApiResponse<List<TeacherDto>>.Ok(resultDtos, "IMPORT_TEACHERS_SUCCESS");
            }
            catch (Exception)
            {
                return ApiResponse<List<TeacherDto>>.Fail("ERR_INTERNAL_SERVER_ERROR", StatusCodes.Status500InternalServerError);
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

                await _repository.FindByCondition(t => t.Id == existingEntity.Id)
                    .ExecuteDeleteAsync();

                return ApiResponse<bool>.Ok(true, "DELETE_TEACHER_SUCCESS");
            }
            catch (Exception)
            {
                return ApiResponse<bool>.Fail("ERR_INTERNAL_SERVER_ERROR", StatusCodes.Status500InternalServerError);
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

                existingEntity.Status = (int)TeacherStatus.Inactive;
                await _repository.UpdateAsync(existingEntity);
                await _repository.SaveChangesAsync();

                return ApiResponse<bool>.Ok(true, "DEACTIVATE_TEACHER_SUCCESS");
            }
            catch (Exception)
            {
                return ApiResponse<bool>.Fail("ERR_INTERNAL_SERVER_ERROR", StatusCodes.Status500InternalServerError);
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
            GradeLevel = entity.GradeLevel.HasValue ? (int)entity.GradeLevel.Value : null,
            GradeLevelName = entity.GradeLevel.HasValue ? entity.GradeLevel.Value.GetStringValue() : null,
            Avatar = entity.Avatar,
            Certificates = DeserializeCertificates(entity.Certificate)
        };

        private static string? SerializeCertificates(IEnumerable<string>? certificates)
        {
            var values = certificates?
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();

            return values.Count == 0 ? null : JsonSerializer.Serialize(values);
        }

        private static List<string> DeserializeCertificates(string? certificate)
        {
            if (string.IsNullOrWhiteSpace(certificate)) return new List<string>();

            try
            {
                var values = JsonSerializer.Deserialize<List<string>>(certificate);
                if (values != null) return values.Where(value => !string.IsNullOrWhiteSpace(value)).ToList();
            }
            catch (JsonException)
            {
                // Existing records stored one certificate URL as plain text.
            }

            return new List<string> { certificate };
        }

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

                var requestedIds = teacherIds.Distinct().ToHashSet();
                var foundIds = teachers.Select(x => x.Id).ToHashSet();
                if (!requestedIds.SetEquals(foundIds))
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
            catch (Exception)
            {
                return ApiResponse<bool>.Fail("ERR_INTERNAL_SERVER_ERROR", StatusCodes.Status500InternalServerError);
            }
        }

        private async Task<string?> ValidateAsync(TeacherSaveDto dto, bool isEdit)
        {
            if (dto == null)
                return "ERR_INVALID_REQUEST";

            dto.Code = dto.Code?.Trim() ?? string.Empty;
            dto.Name = dto.Name?.Trim() ?? string.Empty;
            dto.Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim();
            dto.Phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim();
            dto.Address = string.IsNullOrWhiteSpace(dto.Address) ? null : dto.Address.Trim();
            dto.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
            dto.Avatar = string.IsNullOrWhiteSpace(dto.Avatar) ? null : dto.Avatar.Trim();
            dto.Certificates = dto.Certificates?
                .Where(x => x != null)
                .Select(x => x.Trim())
                .ToList() ?? new List<string>();

            if (isEdit && dto.Id <= 0)
                return "ERR_TEACHER_NOT_FOUND";

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

            if (!string.IsNullOrWhiteSpace(dto.Email))
            {
                if (!MailAddress.TryCreate(dto.Email, out var parsedEmail)
                    || !string.Equals(parsedEmail.Address, dto.Email, StringComparison.OrdinalIgnoreCase))
                {
                    return "ERR_EMAIL_INVALID";
                }
            }

            if (dto.Phone != null && dto.Phone.Length > 20)
                return "ERR_PHONE_MAX_LENGTH";

            if (!string.IsNullOrWhiteSpace(dto.Phone)
                && !Regex.IsMatch(dto.Phone.Trim(), @"^\+?[0-9][0-9\s().-]{6,19}$"))
                return "ERR_PHONE_INVALID";

            if (dto.Dob.HasValue && dto.Dob.Value.Date > DateTime.UtcNow.Date)
                return "ERR_DOB_FUTURE";

            if (dto.Address != null && dto.Address.Length > 500)
                return "ERR_ADDRESS_MAX_LENGTH";

            if (dto.Avatar != null && dto.Avatar.Length > 500)
                return "ERR_AVATAR_MAX_LENGTH";

            if (!Enum.IsDefined(typeof(TeacherStatus), dto.Status))
                return "ERR_TEACHER_STATUS_INVALID";

            if (dto.Certificates?.Any(string.IsNullOrWhiteSpace) == true)
                return "ERR_CERTIFICATE_INVALID";

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

            if (dto.GradeLevel.HasValue && !Enum.IsDefined(typeof(GradeLevel), dto.GradeLevel.Value))
                return "ERR_TEACHER_GRADE_LEVEL_INVALID";

            return null;
        }
    }
}



