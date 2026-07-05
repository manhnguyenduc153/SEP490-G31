
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
                    var keyword = searchDto.Keyword.Trim();
                    query = query.Where(c => c.TextSearch != null && c.TextSearch.Contains(keyword));
                }

                if (searchDto.TeacherStatus.HasValue)
                {
                    query = query.Where(c => c.Status == searchDto.TeacherStatus.Value);
                }

                if (searchDto.GradeLevel.HasValue)
                {
                    query = query.Where(c => c.GradeLevel == searchDto.GradeLevel.Value);
                }

                if (searchDto.Gender.HasValue)
                {
                    query = query.Where(c => c.Gender == searchDto.Gender.Value);
                }

                var totalRecords = await query.CountAsync();
                var entities = await query.ApplyPagingAsync(searchDto);

                var dtos = entities.Select(MapToDto);
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

                return ApiResponse<TeacherDto>.Ok(MapToDto(entity), "GET_TEACHER_DETAIL_SUCCESS");
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
                entity.TextSearch = dto.TextSearch;

                await _repository.AddAsync(entity);
                await _repository.SaveChangesAsync();

                return ApiResponse<TeacherDto>.Created(MapToDto(entity), "CREATE_TEACHER_SUCCESS");
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

                dto.Adapt(existingEntity);
                existingEntity.TextSearch = dto.TextSearch;

                await _repository.UpdateAsync(existingEntity);
                await _repository.SaveChangesAsync();

                return ApiResponse<TeacherDto>.Ok(MapToDto(existingEntity), "UPDATE_TEACHER_SUCCESS");
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
                    entity.TextSearch = dto.TextSearch;

                    // Tự động tạo IdentityUser cho Teacher
                    var user = new IdentityUser
                    {
                        UserName = entity.Email ?? $"teacher_{Guid.NewGuid():N}",
                        Email = entity.Email,
                        EmailConfirmed = true
                    };

                    var result = await _userManager.CreateAsync(user, "123456"); // Mật khẩu mặc định
                    if (result.Succeeded)
                    {
                        var roleExists = await _roleManager.RoleExistsAsync("Teacher");
                        if (roleExists)
                        {
                            await _userManager.AddToRoleAsync(user, "Teacher");
                        }
                    }

                    await _repository.AddAsync(entity);
                    createdTeachers.Add(entity);
                }

                await _repository.SaveChangesAsync();

                var resultDtos = createdTeachers.Select(MapToDto).ToList();
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


