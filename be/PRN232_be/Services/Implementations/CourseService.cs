using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Mapster;
using PRN232_be.DTO;
using PRN232_be.DTO.Course;
using PRN232_be.Models;
using PRN232_be.Repositories.Interfaces;
using PRN232_be.Services.Interfaces;
using PRN232_be.Helpers;

namespace PRN232_be.Services.Implementations
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
                    query = query.Where(c => c.Status == searchDto.Status.Value);
                }

                var totalRecords = await query.CountAsync();
                var entities = await query.ApplyPagingAsync(searchDto.PageIndex, searchDto.PageSize);

                var dtos = entities.Select(MapToDto);
                var pagingResponse = dtos.ToPagingResponse(totalRecords, searchDto.PageIndex, searchDto.PageSize);

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

        // ===================== PRIVATE MAPPING =====================

        private static CourseDto MapToDto(Course entity) => new()
        {
            Id = entity.Id,
            Code = entity.Code ?? string.Empty,
            Name = entity.Name ?? string.Empty,
            Status = entity.Status,
            Duration = entity.Duration,
            Price = entity.Price,
            Description = entity.Description
        };

        // ===================== PRIVATE VALIDATE =====================

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

            if (dto.Price.HasValue && dto.Price.Value < 0)
                return "ERR_PRICE_NEGATIVE";

            if (dto.Duration.HasValue && dto.Duration.Value < 0)
                return "ERR_DURATION_NEGATIVE";

            // Kiểm tra trùng mã khóa học
            var duplicateCode = await _repository.FindAll()
                .FirstOrDefaultAsync(c => c.Code == dto.Code && (!isEdit || c.Id != dto.Id));

            if (duplicateCode != null)
                return "ERR_CODE_DUPLICATE";

            // Kiểm tra trùng tên khóa học
            var duplicateName = await _repository.FindAll()
                .FirstOrDefaultAsync(c => c.Name == dto.Name && (!isEdit || c.Id != dto.Id));

            if (duplicateName != null)
                return "ERR_NAME_DUPLICATE";

            return null;
        }
    }
}
