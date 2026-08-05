using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Mapster;
using sep490_be.DTO;
using sep490_be.DTO.Student;
using sep490_be.Models;
using sep490_be.Enums;
using sep490_be.Repositories.Interfaces;
using sep490_be.Services.Interfaces;
using sep490_be.Helpers;
using Microsoft.AspNetCore.Identity;


namespace sep490_be.Services.Implementations
{
    public class StudentService : IStudentService
    {
        private readonly IStudentRepository _repository;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public StudentService(
            IStudentRepository repository,
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _repository = repository;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task<ApiResponse<PagingResponse<StudentDto>>> GetAllAsync(StudentSearchDto searchDto)
        {
            try
            {
                var query = _repository.FindAll();

                // Keyword search using TextSearch field
                if (!string.IsNullOrWhiteSpace(searchDto.Keyword))
                {
                    query = query.Where(s => s.TextSearch != null && s.TextSearch.Contains(searchDto.Keyword));
                }

                // Filtering by StudentStatus
                if (searchDto.StudentStatus.HasValue)
                {
                    query = query.Where(s => s.Status == searchDto.StudentStatus.Value);
                }

                // Filtering by GradeLevel
                if (searchDto.GradeLevel.HasValue)
                {
                    query = query.Where(s => s.GradeLevel == searchDto.GradeLevel.Value);
                }

                // Filtering by Gender
                if (searchDto.Gender.HasValue)
                {
                    query = query.Where(s => s.Gender == searchDto.Gender.Value);
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

                return ApiResponse<PagingResponse<StudentDto>>.Ok(pagingResponse, "GET_STUDENT_LIST_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<PagingResponse<StudentDto>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<StudentDto>> GetByIdAsync(int id)
        {
            try
            {
                var entity = await _repository.GetByIdAsync(id);
                if (entity == null)
                {
                    return ApiResponse<StudentDto>.Fail("ERR_STUDENT_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                var dto = MapToDto(entity);
                if (!string.IsNullOrEmpty(dto.Email))
                {
                    dto.HasAccount = await _userManager.Users.AnyAsync(u => u.Email == dto.Email);
                }

                return ApiResponse<StudentDto>.Ok(dto, "GET_STUDENT_DETAIL_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<StudentDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<StudentDto>> CreateAsync(StudentSaveDto dto)
        {
            try
            {
                var validationError = await ValidateAsync(dto, isEdit: false);
                if (validationError != null)
                {
                    return ApiResponse<StudentDto>.Fail(validationError, StatusCodes.Status400BadRequest);
                }

                var entity = dto.Adapt<Student>();
                entity.Id = 0;

                await _repository.AddAsync(entity);
                await _repository.SaveChangesAsync();

                // Automatically provision user account
                if (!string.IsNullOrWhiteSpace(entity.Email))
                {
                    var emailTrimmed = entity.Email.Trim();
                    
                    if (!await _roleManager.RoleExistsAsync("Student"))
                    {
                        await _roleManager.CreateAsync(new IdentityRole("Student"));
                    }

                    var existingUser = await _userManager.FindByEmailAsync(emailTrimmed);
                    if (existingUser == null)
                    {
                        var identityUser = new IdentityUser
                        {
                            UserName = emailTrimmed,
                            Email = emailTrimmed,
                            PhoneNumber = entity.Phone?.Trim(),
                            EmailConfirmed = true
                        };

                        var userResult = await _userManager.CreateAsync(identityUser, "123456");
                        if (userResult.Succeeded)
                        {
                            await _userManager.AddToRoleAsync(identityUser, "Student");
                        }
                        else
                        {
                            var errMsgs = string.Join(", ", userResult.Errors.Select(e => e.Description));
                            return ApiResponse<StudentDto>.Fail($"ERR_ACCOUNT_CREATION_FAILED: {errMsgs}", StatusCodes.Status400BadRequest);
                        }
                    }
                    else
                    {
                        if (!await _userManager.IsInRoleAsync(existingUser, "Student"))
                        {
                            await _userManager.AddToRoleAsync(existingUser, "Student");
                        }
                    }
                }

                var resultDto = MapToDto(entity);
                if (!string.IsNullOrEmpty(resultDto.Email))
                {
                    resultDto.HasAccount = await _userManager.Users.AnyAsync(u => u.Email == resultDto.Email);
                }

                return ApiResponse<StudentDto>.Created(resultDto, "CREATE_STUDENT_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<StudentDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<StudentDto>> EditAsync(StudentSaveDto dto)
        {
            try
            {
                var validationError = await ValidateAsync(dto, isEdit: true);
                if (validationError != null)
                {
                    return ApiResponse<StudentDto>.Fail(validationError, StatusCodes.Status400BadRequest);
                }

                var existingEntity = await _repository.GetByIdAsync(dto.Id);
                if (existingEntity == null)
                {
                    return ApiResponse<StudentDto>.Fail("ERR_STUDENT_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                var oldEmail = existingEntity.Email;
                dto.Adapt(existingEntity);

                await _repository.UpdateAsync(existingEntity);
                await _repository.SaveChangesAsync();

                // If profile is active, make sure user is not locked out
                if (existingEntity.Status == (int)StudentStatus.Active && !string.IsNullOrEmpty(existingEntity.Email))
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

                return ApiResponse<StudentDto>.Ok(resultDto, "UPDATE_STUDENT_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<StudentDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<bool>> DeleteAsync(int id)
        {
            try
            {
                var existingEntity = await _repository.GetByIdAsync(id);
                if (existingEntity == null)
                {
                    return ApiResponse<bool>.Fail("ERR_STUDENT_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                if (await _repository.IsInUseAsync(id))
                {
                    return ApiResponse<bool>.Fail("ERR_STUDENT_IN_USE", StatusCodes.Status400BadRequest);
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

                // Real removal from DB (not IsDeleted = true) — see IStudentRepository.HardDeleteAsync.
                await _repository.HardDeleteAsync(id);

                return ApiResponse<bool>.Ok(true, "DELETE_STUDENT_SUCCESS");
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
                    return ApiResponse<bool>.Fail("ERR_STUDENT_NOT_FOUND", StatusCodes.Status404NotFound);
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

                return ApiResponse<bool>.Ok(true, "DEACTIVATE_STUDENT_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<Dictionary<string, int>>> CheckEmailsAsync(List<string> emails)
        {
            try
            {
                if (emails == null || !emails.Any())
                {
                    return ApiResponse<Dictionary<string, int>>.Ok(new Dictionary<string, int>(), "CHECK_EMAILS_SUCCESS");
                }

                var cleanEmails = emails.Where(e => !string.IsNullOrWhiteSpace(e)).Select(e => e.Trim().ToLower()).ToList();

                var existingStudents = await _repository.FindAll()
                    .Where(s => s.Email != null && cleanEmails.Contains(s.Email.ToLower()))
                    .Select(s => new { s.Email, s.Id })
                    .ToListAsync();

                var result = existingStudents
                    .GroupBy(s => s.Email!.ToLower())
                    .ToDictionary(g => g.Key, g => g.First().Id);

                return ApiResponse<Dictionary<string, int>>.Ok(result, "CHECK_EMAILS_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<Dictionary<string, int>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        // ===================== PRIVATE MAPPING =====================

        private static StudentDto MapToDto(Student entity)
        {
            string statusName = "Unknown";
            if (Enum.IsDefined(typeof(StudentStatus), entity.Status))
            {
                statusName = ((StudentStatus)entity.Status).GetStringValue();
            }

            return new StudentDto
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
                StatusName = statusName,
                Description = entity.Description,
                SchoolName = entity.SchoolName,
                GradeLevel = entity.GradeLevel,
                ParentName = entity.ParentName,
                ParentPhone = entity.ParentPhone,
                Avatar = entity.Avatar
            };
        }

        public async Task<ApiResponse<List<StudentDto>>> ImportAsync(List<StudentSaveDto> dtos)
        {
            var results = new List<StudentDto>();
            var errors = new List<string>();
            
            foreach (var dto in dtos)
            {
                try 
                {
                    if (string.IsNullOrWhiteSpace(dto.Code))
                    {
                        dto.Code = $"ST_{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";
                    }

                    var res = await CreateAsync(dto);
                    if (res.Success && res.Data != null)
                    {
                        results.Add(res.Data);
                    }
                    else
                    {
                        errors.Add($"Học sinh '{dto.Name}' (Email: {dto.Email}): {res.Message}");
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Học sinh '{dto.Name}': {ex.Message}");
                }
            }

            if (errors.Any())
            {
                var combinedMessage = string.Join("; ", errors);
                if (results.Any())
                {
                    return ApiResponse<List<StudentDto>>.Ok(results, $"IMPORT_PARTIAL_SUCCESS: {combinedMessage}");
                }
                return ApiResponse<List<StudentDto>>.Fail($"ERR_IMPORT_FAILED: {combinedMessage}", StatusCodes.Status400BadRequest);
            }

            return ApiResponse<List<StudentDto>>.Ok(results, "IMPORT_STUDENT_SUCCESS");
        }

        public async Task<ApiResponse<bool>> BulkProvisionAccountsAsync(List<int> studentIds)
        {
            try
            {
                if (studentIds == null || !studentIds.Any())
                {
                    return ApiResponse<bool>.Fail("ERR_NO_STUDENTS_SELECTED", StatusCodes.Status400BadRequest);
                }

                var students = await _repository.FindAll()
                    .Where(s => studentIds.Contains(s.Id) && !s.IsDeleted)
                    .ToListAsync();

                if (!students.Any())
                {
                    return ApiResponse<bool>.Fail("ERR_STUDENTS_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                if (!await _roleManager.RoleExistsAsync("Student"))
                {
                    await _roleManager.CreateAsync(new IdentityRole("Student"));
                }

                var errors = new List<string>();
                int successCount = 0;

                foreach (var student in students)
                {
                    if (string.IsNullOrWhiteSpace(student.Email))
                    {
                        errors.Add($"Học sinh '{student.Name}' (Mã: {student.Code}) không có email.");
                        continue;
                    }

                    var emailTrimmed = student.Email.Trim();

                    // Check if account already exists for this email
                    var existingUser = await _userManager.FindByEmailAsync(emailTrimmed);
                    if (existingUser != null)
                    {
                        // Account already exists, check role
                        if (!await _userManager.IsInRoleAsync(existingUser, "Student"))
                        {
                            await _userManager.AddToRoleAsync(existingUser, "Student");
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
                        PhoneNumber = student.Phone?.Trim(),
                        EmailConfirmed = true
                    };

                    var userResult = await _userManager.CreateAsync(identityUser, "123456");
                    if (userResult.Succeeded)
                    {
                        await _userManager.AddToRoleAsync(identityUser, "Student");
                        successCount++;
                    }
                    else
                    {
                        var errMsgs = string.Join(", ", userResult.Errors.Select(e => e.Description));
                        errors.Add($"Học sinh '{student.Name}' (Email: {emailTrimmed}) lỗi: {errMsgs}");
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

        // ===================== PRIVATE VALIDATE =====================

        private async Task<string?> ValidateAsync(StudentSaveDto dto, bool isEdit)
        {
            if (string.IsNullOrWhiteSpace(dto.Code))
                return "ERR_CODE_EMPTY";

            if (dto.Code.Length > 50)
                return "ERR_CODE_MAX_LENGTH";

            if (string.IsNullOrWhiteSpace(dto.Name))
                return "ERR_NAME_EMPTY";

            if (dto.Name.Length > 200)
                return "ERR_NAME_MAX_LENGTH";

            if (string.IsNullOrWhiteSpace(dto.Email))
                return "ERR_EMAIL_EMPTY";

            if (dto.Email.Length > 150)
                return "ERR_EMAIL_MAX_LENGTH";

            if (dto.Phone != null && dto.Phone.Length > 20)
                return "ERR_PHONE_MAX_LENGTH";

            if (dto.Address != null && dto.Address.Length > 500)
                return "ERR_ADDRESS_MAX_LENGTH";

            if (dto.SchoolName != null && dto.SchoolName.Length > 200)
                return "ERR_SCHOOL_NAME_MAX_LENGTH";

            if (dto.ParentName != null && dto.ParentName.Length > 200)
                return "ERR_PARENT_NAME_MAX_LENGTH";

            if (dto.ParentPhone != null && dto.ParentPhone.Length > 20)
                return "ERR_PARENT_PHONE_MAX_LENGTH";

            // Check duplicate Code
            var duplicateCode = await _repository.FindAll()
                .FirstOrDefaultAsync(s => s.Code == dto.Code && (!isEdit || s.Id != dto.Id));

            if (duplicateCode != null)
                return "ERR_CODE_DUPLICATE";

            // Check duplicate Email in Student repository
            if (!string.IsNullOrWhiteSpace(dto.Email))
            {
                var duplicateEmail = await _repository.FindAll()
                    .FirstOrDefaultAsync(s => s.Email == dto.Email && (!isEdit || s.Id != dto.Id));

                if (duplicateEmail != null)
                    return "ERR_EMAIL_DUPLICATE";
            }

            // Check duplicate Email in Identity Users for new creations
            if (!isEdit && !string.IsNullOrWhiteSpace(dto.Email))
            {
                var identityUser = await _userManager.FindByEmailAsync(dto.Email.Trim());
                if (identityUser != null)
                    return "ERR_EMAIL_DUPLICATE";
            }

            return null;
        }
    }
}

