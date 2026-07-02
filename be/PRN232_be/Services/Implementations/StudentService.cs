using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Mapster;
using PRN232_be.DTO;
using PRN232_be.DTO.Student;
using PRN232_be.Models;
using PRN232_be.Enums;
using PRN232_be.Repositories.Interfaces;
using PRN232_be.Services.Interfaces;
using PRN232_be.Helpers;
using Microsoft.AspNetCore.Identity;


namespace PRN232_be.Services.Implementations
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

                return ApiResponse<StudentDto>.Ok(MapToDto(entity), "GET_STUDENT_DETAIL_SUCCESS");
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

                // Tạo tài khoản IdentityUser cho học sinh
                var emailTrimmed = dto.Email!.Trim();
                var identityUser = new IdentityUser
                {
                    UserName = emailTrimmed, // username là email
                    Email = emailTrimmed,
                    PhoneNumber = dto.Phone?.Trim(),
                    EmailConfirmed = true
                };

                var userResult = await _userManager.CreateAsync(identityUser, "123456");
                if (!userResult.Succeeded)
                {
                    var errors = string.Join(", ", userResult.Errors.Select(e => e.Description));
                    return ApiResponse<StudentDto>.Fail($"ERR_CREATE_USER_FAILED: {errors}", StatusCodes.Status400BadRequest);
                }

                if (!await _roleManager.RoleExistsAsync("Student"))
                {
                    await _roleManager.CreateAsync(new IdentityRole("Student"));
                }
                await _userManager.AddToRoleAsync(identityUser, "Student");

                var entity = dto.Adapt<Student>();
                entity.Id = 0;

                await _repository.AddAsync(entity);
                await _repository.SaveChangesAsync();

                return ApiResponse<StudentDto>.Created(MapToDto(entity), "CREATE_STUDENT_SUCCESS");
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

                dto.Adapt(existingEntity);

                await _repository.UpdateAsync(existingEntity);
                await _repository.SaveChangesAsync();

                return ApiResponse<StudentDto>.Ok(MapToDto(existingEntity), "UPDATE_STUDENT_SUCCESS");
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

                await _repository.DeleteAsync(existingEntity);
                await _repository.SaveChangesAsync();

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
