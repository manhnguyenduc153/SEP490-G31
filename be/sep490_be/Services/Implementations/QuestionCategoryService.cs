using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Mapster;
using sep490_be.DTO;
using sep490_be.DTO.QuestionCategory;
using sep490_be.Models;
using sep490_be.Repositories.Interfaces;
using sep490_be.Services.Interfaces;
using sep490_be.Helpers;
using sep490_be.Enums;

namespace sep490_be.Services.Implementations
{
    public class QuestionCategoryService : IQuestionCategoryService
    {
        private readonly IQuestionCategoryRepository _repository;

        public QuestionCategoryService(IQuestionCategoryRepository repository)
        {
            _repository = repository;
        }

        public async Task<ApiResponse<PagingResponse<QuestionCategoryDto>>> GetAllAsync(QuestionCategorySearchDto searchDto)
        {
            try
            {
                var query = _repository.FindAll().Include(c => c.Course).AsQueryable();

                if (!string.IsNullOrWhiteSpace(searchDto.Keyword))
                {
                    query = query.Where(c => c.TextSearch != null && c.TextSearch.Contains(searchDto.Keyword));
                }

                if (searchDto.CourseId.HasValue)
                {
                    query = query.Where(c => c.CourseId == searchDto.CourseId.Value);
                }

                var totalRecords = await query.CountAsync();
                var entities = await query.ApplyPagingAsync(searchDto);

                var dtos = entities.Select(MapToDto);
                var pagingResponse = dtos.ToPagingResponse(totalRecords, searchDto);

                return ApiResponse<PagingResponse<QuestionCategoryDto>>.Ok(pagingResponse, "GET_CATEGORY_LIST_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<PagingResponse<QuestionCategoryDto>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<QuestionCategoryDto>> GetByIdAsync(int id)
        {
            try
            {
                var entity = await _repository.FindAll()
                    .Include(c => c.Course)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (entity == null)
                {
                    return ApiResponse<QuestionCategoryDto>.Fail("ERR_CATEGORY_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                return ApiResponse<QuestionCategoryDto>.Ok(MapToDto(entity), "GET_CATEGORY_DETAIL_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<QuestionCategoryDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<QuestionCategoryDto>> CreateAsync(QuestionCategorySaveDto dto)
        {
            try
            {
                var validationError = await ValidateAsync(dto, isEdit: false);
                if (validationError != null)
                {
                    return ApiResponse<QuestionCategoryDto>.Fail(validationError, StatusCodes.Status400BadRequest);
                }

                var entity = dto.Adapt<QuestionCategory>();
                entity.Id = 0;
                entity.CourseId = dto.CourseId;

                await _repository.AddAsync(entity);
                await _repository.SaveChangesAsync();

                return await GetByIdAsync(entity.Id);
            }
            catch (Exception ex)
            {
                return ApiResponse<QuestionCategoryDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<QuestionCategoryDto>> EditAsync(QuestionCategorySaveDto dto)
        {
            try
            {
                var validationError = await ValidateAsync(dto, isEdit: true);
                if (validationError != null)
                {
                    return ApiResponse<QuestionCategoryDto>.Fail(validationError, StatusCodes.Status400BadRequest);
                }

                var existingEntity = await _repository.GetByIdAsync(dto.Id);
                if (existingEntity == null)
                {
                    return ApiResponse<QuestionCategoryDto>.Fail("ERR_CATEGORY_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                existingEntity.Code = dto.Code;
                existingEntity.Name = dto.Name;
                existingEntity.Description = dto.Description;
                existingEntity.CourseId = dto.CourseId;

                await _repository.UpdateAsync(existingEntity);
                await _repository.SaveChangesAsync();

                return await GetByIdAsync(existingEntity.Id);
            }
            catch (Exception ex)
            {
                return ApiResponse<QuestionCategoryDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<bool>> DeleteAsync(int id)
        {
            try
            {
                var existingEntity = await _repository.GetByIdAsync(id);
                if (existingEntity == null)
                {
                    return ApiResponse<bool>.Fail("ERR_CATEGORY_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                if (await _repository.IsUsedInQuestionsAsync(id))
                {
                    return ApiResponse<bool>.Fail("ERR_CATEGORY_IN_USE", StatusCodes.Status400BadRequest);
                }

                // Real removal from DB (not IsDeleted = true) — see IQuestionCategoryRepository.HardDeleteAsync.
                await _repository.HardDeleteAsync(id);

                return ApiResponse<bool>.Ok(true, "DELETE_CATEGORY_SUCCESS");
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
                    return ApiResponse<bool>.Fail("ERR_CATEGORY_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                await _repository.DeactiveAsync(existingEntity);
                await _repository.SaveChangesAsync();

                return ApiResponse<bool>.Ok(true, "DEACTIVATE_CATEGORY_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        // ===================== PRIVATE MAPPING =====================

        /// <summary>
        /// Map thủ công Entity → DTO.
        /// Tập trung toàn bộ logic mapping ở đây để dễ mở rộng:
        /// - Thêm StatusName (string value của enum)
        /// - Format, tính toán thêm field phía C#
        /// </summary>
        private static QuestionCategoryDto MapToDto(QuestionCategory entity) => new()
        {
            Id = entity.Id,
            Code = entity.Code ?? string.Empty,
            Name = entity.Name ?? string.Empty,
            Description = entity.Description,
            CourseId = entity.CourseId,
            CourseName = entity.Course?.Name,
            CourseCode = entity.Course?.Code,
        };

        // ===================== PRIVATE VALIDATE =====================

        private async Task<string?> ValidateAsync(QuestionCategorySaveDto dto, bool isEdit)
        {
            if (string.IsNullOrWhiteSpace(dto.Code))
                return "ERR_CODE_EMPTY";

            if (dto.Code.Length > 50)
                return "ERR_CODE_MAX_LENGTH";

            if (string.IsNullOrWhiteSpace(dto.Name))
                return "ERR_NAME_EMPTY";

            if (dto.Name.Length > 200)
                return "ERR_NAME_MAX_LENGTH";

            if (dto.Description != null && dto.Description.Length > 500)
                return "ERR_DESC_MAX_LENGTH";

            // Kiểm tra trùng mã
            var duplicateCode = await _repository.FindAll()
                .FirstOrDefaultAsync(c => c.Code == dto.Code && (!isEdit || c.Id != dto.Id));

            if (duplicateCode != null)
                return "ERR_CODE_DUPLICATE";

            // Kiểm tra trùng tên
            var duplicateName = await _repository.FindAll()
                .FirstOrDefaultAsync(c => c.Name == dto.Name && (!isEdit || c.Id != dto.Id));

            if (duplicateName != null)
                return "ERR_NAME_DUPLICATE";

            return null;
        }
    }
}

