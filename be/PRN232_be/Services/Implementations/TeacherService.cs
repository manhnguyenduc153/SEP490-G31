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

namespace PRN232_be.Services.Implementations
{
    public class TeacherService : ITeacherService
    {
        private readonly ITeacherRepository _repository;

        public TeacherService(ITeacherRepository repository)
        {
            _repository = repository;
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

                await _repository.UpdateAsync(existingEntity);
                await _repository.SaveChangesAsync();

                return ApiResponse<TeacherDto>.Ok(MapToDto(existingEntity), "UPDATE_TEACHER_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<TeacherDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
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
