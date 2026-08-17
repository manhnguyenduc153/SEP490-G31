using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Mapster;
using sep490_be.DTO;
using sep490_be.DTO.Course;
using sep490_be.Models;
using sep490_be.Repositories.Interfaces;
using sep490_be.Services.Interfaces;
using sep490_be.Helpers;
using sep490_be.Enums;

namespace sep490_be.Services.Implementations
{
    public class CourseService : ICourseService
    {
        private readonly ICourseRepository _repository;

        public CourseService(ICourseRepository repository)
        {
            _repository = repository;
        }

        public async Task<ApiResponse<PagingResponse<CourseDto>>> GetAllAsync(CourseSearchDto searchDto)
        {
            try
            {
                var query = _repository.FindAll();

                if (!string.IsNullOrWhiteSpace(searchDto.Keyword))
                {
                    query = query.Where(c => c.TextSearch != null && c.TextSearch.Contains(searchDto.Keyword));
                }

                if (searchDto.Status.HasValue)
                {
                    var statusValue = searchDto.Status.Value ? 1 : 0;
                    query = query.Where(c => c.Status == statusValue);
                }

                var totalRecords = await query.CountAsync();
                var entities = await query.OrderByDescending(c => c.Id).ApplyPagingAsync(searchDto);

                var dtos = entities.Select(MapToDto);
                var pagingResponse = dtos.ToPagingResponse(totalRecords, searchDto);

                return ApiResponse<PagingResponse<CourseDto>>.Ok(pagingResponse, "GET_COURSE_LIST_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<PagingResponse<CourseDto>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<CourseDto>> GetByIdAsync(int id)
        {
            try
            {
                var entity = await _repository.GetByIdAsync(id);
                if (entity == null)
                {
                    return ApiResponse<CourseDto>.Fail("ERR_COURSE_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                return ApiResponse<CourseDto>.Ok(MapToDto(entity), "GET_COURSE_DETAIL_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<CourseDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<CourseDto>> CreateAsync(CourseSaveDto dto)
        {
            try
            {
                var validationError = await ValidateAsync(dto, isEdit: false);
                if (validationError != null)
                {
                    return ApiResponse<CourseDto>.Fail(validationError, StatusCodes.Status400BadRequest);
                }

                var entity = dto.Adapt<Course>();
                entity.Id = 0;
                entity.Status = dto.Status != 0 ? dto.Status : 1; // Default to Active (1) if not set

                await _repository.AddAsync(entity);
                await _repository.SaveChangesAsync();

                return ApiResponse<CourseDto>.Created(MapToDto(entity), "CREATE_COURSE_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<CourseDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<CourseDto>> EditAsync(CourseSaveDto dto)
        {
            try
            {
                var validationError = await ValidateAsync(dto, isEdit: true);
                if (validationError != null)
                {
                    return ApiResponse<CourseDto>.Fail(validationError, StatusCodes.Status400BadRequest);
                }

                var existingEntity = await _repository.GetByIdAsync(dto.Id);
                if (existingEntity == null)
                {
                    return ApiResponse<CourseDto>.Fail("ERR_COURSE_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                dto.Adapt(existingEntity);

                await _repository.UpdateAsync(existingEntity);
                await _repository.SaveChangesAsync();

                return ApiResponse<CourseDto>.Ok(MapToDto(existingEntity), "UPDATE_COURSE_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<CourseDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<bool>> DeleteAsync(int id)
        {
            try
            {
                var existingEntity = await _repository.GetByIdAsync(id);
                if (existingEntity == null)
                {
                    return ApiResponse<bool>.Fail("ERR_COURSE_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                await _repository.DeleteAsync(existingEntity);
                await _repository.SaveChangesAsync();

                return ApiResponse<bool>.Ok(true, "DELETE_COURSE_SUCCESS");
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
                    return ApiResponse<bool>.Fail("ERR_COURSE_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                await _repository.DeactiveAsync(existingEntity);
                await _repository.SaveChangesAsync();

                return ApiResponse<bool>.Ok(true, "DEACTIVATE_COURSE_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        private static CourseDto MapToDto(Course entity) => new()
        {
            Id = entity.Id,
            Code = entity.Code ?? string.Empty,
            Name = entity.Name ?? string.Empty,
            Status = entity.Status,
            StatusName = ((GeneralStatus)entity.Status).GetStringValue(),
            Duration = entity.Duration,
            Price = entity.Price,
            Description = entity.Description,
            RequiredGradeLevel = entity.RequiredGradeLevel.HasValue ? (int)entity.RequiredGradeLevel.Value : null,
            RequiredGradeLevelName = entity.RequiredGradeLevel.HasValue ? entity.RequiredGradeLevel.Value.GetStringValue() : null
        };

        private async Task<string?> ValidateAsync(CourseSaveDto dto, bool isEdit)
        {
            if (string.IsNullOrWhiteSpace(dto.Code))
                return "ERR_CODE_EMPTY";

            if (dto.Code.Length > 50)
                return "ERR_CODE_MAX_LENGTH";

            if (string.IsNullOrWhiteSpace(dto.Name))
                return "ERR_NAME_EMPTY";

            if (dto.Name.Length > 200)
                return "ERR_NAME_MAX_LENGTH";

            if (dto.Description != null && dto.Description.Length > 1000)
                return "ERR_DESC_MAX_LENGTH";
            if (dto.Duration.HasValue && dto.Duration.Value <= 0)
                return "ERR_DURATION_INVALID";

            if (dto.Price.HasValue && dto.Price.Value < 0)
                return "ERR_PRICE_INVALID";

            if (dto.RequiredGradeLevel.HasValue && !Enum.IsDefined(typeof(GradeLevel), dto.RequiredGradeLevel.Value))
                return "ERR_COURSE_GRADE_LEVEL_INVALID";

            var (codeExists, nameExists) = await ValidationHelper.CheckDuplicateCodeAndNameAsync(_repository, isEdit ? dto.Id : (int?)null, dto.Code, dto.Name);
            if (codeExists)
                return "ERR_CODE_DUPLICATE";

            if (nameExists)
                return "ERR_NAME_DUPLICATE";

            return null;
        }
    }
}
