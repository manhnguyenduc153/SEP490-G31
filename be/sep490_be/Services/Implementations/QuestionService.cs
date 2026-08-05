using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using sep490_be.DTO;
using sep490_be.DTO.Question;
using sep490_be.Models;
using sep490_be.Repositories.Interfaces;
using sep490_be.Services.Interfaces;
using sep490_be.Helpers;
using sep490_be.Enums;

namespace sep490_be.Services.Implementations
{
    public class QuestionService : IQuestionService
    {
        private readonly IQuestionRepository _repository;
        private readonly IQuestionCategoryRepository _categoryRepository;
        private readonly ApplicationDbContext _dbContext;

        public QuestionService(
            IQuestionRepository repository,
            IQuestionCategoryRepository categoryRepository,
            ApplicationDbContext dbContext = null)
        {
            _repository = repository;
            _categoryRepository = categoryRepository;
            _dbContext = dbContext;
        }

        public async Task<ApiResponse<PagingResponse<QuestionDto>>> GetAllAsync(QuestionSearchDto searchDto)
        {
            try
            {
                var query = _repository.FindAll()
                    .Include(q => q.QuestionCategory)
                    .Include(q => q.QuestionAnswers)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(searchDto.Keyword))
                {
                    query = query.Where(q => (q.TextSearch != null && q.TextSearch.Contains(searchDto.Keyword)) 
                                             || q.Name.Contains(searchDto.Keyword) 
                                             || q.Content.Contains(searchDto.Keyword)
                                             || (q.Code != null && q.Code.Contains(searchDto.Keyword)));
                }

                if (searchDto.CategoryId.HasValue)
                {
                    query = query.Where(q => q.CategoryId == searchDto.CategoryId.Value);
                }

                if (searchDto.DifficultyLevel.HasValue)
                {
                    query = query.Where(q => q.DifficultyLevel == searchDto.DifficultyLevel.Value);
                }

                if (searchDto.QuestionType.HasValue)
                {
                    query = query.Where(q => q.QuestionType == searchDto.QuestionType.Value);
                }

                if (searchDto.SkillType.HasValue)
                {
                    query = query.Where(q => q.SkillType == searchDto.SkillType.Value);
                }



                var totalRecords = await query.CountAsync();
                var entities = await query.ApplyPagingAsync(searchDto);

                var dtos = entities.Select(MapToDto).ToList();
                var pagingResponse = dtos.ToPagingResponse(totalRecords, searchDto);

                return ApiResponse<PagingResponse<QuestionDto>>.Ok(pagingResponse, "GET_QUESTION_LIST_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<PagingResponse<QuestionDto>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<QuestionDto>> GetByIdAsync(int id)
        {
            try
            {
                var entity = await _repository.FindAll()
                    .Include(q => q.QuestionCategory)
                    .Include(q => q.QuestionAnswers)
                    .FirstOrDefaultAsync(q => q.Id == id);

                if (entity == null)
                {
                    return ApiResponse<QuestionDto>.Fail("ERR_QUESTION_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                return ApiResponse<QuestionDto>.Ok(MapToDto(entity), "GET_QUESTION_DETAIL_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<QuestionDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<QuestionDto>> CreateAsync(QuestionSaveDto dto)
        {
            try
            {
                var validationError = await ValidateAsync(dto, isEdit: false);
                if (validationError != null)
                {
                    return ApiResponse<QuestionDto>.Fail(validationError, StatusCodes.Status400BadRequest);
                }

                int effectiveSkillType = dto.SkillType > 0 ? dto.SkillType : (int)SkillType.Listening;

                var entity = new Question
                {
                    Id = 0,
                    Code = string.IsNullOrWhiteSpace(dto.Code) ? $"Q_{Guid.NewGuid().ToString().Substring(0, 8)}" : dto.Code,
                    Name = dto.Name,
                    Content = dto.Content,
                    QuestionType = dto.QuestionType,
                    SkillType = effectiveSkillType,
                    DifficultyLevel = dto.DifficultyLevel,
                    Explanation = dto.Explanation,
                    MediaUrl = dto.MediaUrl,
                    CategoryId = dto.CategoryId,
                    Point = dto.Point ?? 0,
                    Status = 1 // Active by default
                };

                if (dto.QuestionType != (int)QuestionType.Essay && dto.QuestionAnswers != null)
                {
                    foreach (var answerDto in dto.QuestionAnswers)
                    {
                        entity.QuestionAnswers.Add(new QuestionAnswer
                        {
                            Content = answerDto.Content,
                            IsCorrect = answerDto.IsCorrect,
                            Code = $"QA_{Guid.NewGuid().ToString().Substring(0, 8)}",
                            Name = answerDto.Content.Length > 200 ? answerDto.Content.Substring(0, 197) + "..." : answerDto.Content
                        });
                    }
                }

                await _repository.AddAsync(entity);
                await _repository.SaveChangesAsync();

                // Load relationships for response DTO mapping
                var savedEntity = await _repository.FindAll()
                    .Include(q => q.QuestionCategory)
                    .Include(q => q.QuestionAnswers)
                    .FirstOrDefaultAsync(q => q.Id == entity.Id);

                return ApiResponse<QuestionDto>.Created(MapToDto(savedEntity!), "CREATE_QUESTION_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<QuestionDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<QuestionDto>> EditAsync(QuestionSaveDto dto)
        {
            try
            {
                var validationError = await ValidateAsync(dto, isEdit: true);
                if (validationError != null)
                {
                    return ApiResponse<QuestionDto>.Fail(validationError, StatusCodes.Status400BadRequest);
                }

                var existingEntity = await _repository.FindAll()
                    .Include(q => q.QuestionAnswers)
                    .FirstOrDefaultAsync(q => q.Id == dto.Id);

                if (existingEntity == null)
                {
                    return ApiResponse<QuestionDto>.Fail("ERR_QUESTION_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                existingEntity.Name = dto.Name;
                existingEntity.Content = dto.Content;
                existingEntity.QuestionType = dto.QuestionType;
                if (dto.SkillType > 0)
                {
                    existingEntity.SkillType = dto.SkillType;
                }
                existingEntity.DifficultyLevel = dto.DifficultyLevel;
                existingEntity.Explanation = dto.Explanation;
                existingEntity.MediaUrl = dto.MediaUrl;
                existingEntity.CategoryId = dto.CategoryId;
                existingEntity.Point = dto.Point ?? 0;
                if (dto.Code != null)
                {
                    existingEntity.Code = dto.Code;
                }

                if (dto.QuestionType == (int)QuestionType.Essay)
                {
                    // For essay, delete all answers
                    foreach (var ans in existingEntity.QuestionAnswers)
                    {
                        ans.IsDeleted = true;
                    }
                }
                else if (dto.QuestionAnswers != null)
                {
                    var existingAnswers = existingEntity.QuestionAnswers.Where(a => !a.IsDeleted).ToList();
                    var incomingAnswers = dto.QuestionAnswers;

                    // Delete answers that are not in the incoming list
                    foreach (var existingAnswer in existingAnswers)
                    {
                        if (!incomingAnswers.Any(a => a.Id == existingAnswer.Id))
                        {
                            existingAnswer.IsDeleted = true;
                        }
                    }

                    // Add or update incoming answers
                    foreach (var incomingAnswer in incomingAnswers)
                    {
                        if (incomingAnswer.Id > 0)
                        {
                            var existingAnswer = existingAnswers.FirstOrDefault(a => a.Id == incomingAnswer.Id);
                            if (existingAnswer != null)
                            {
                                existingAnswer.Content = incomingAnswer.Content;
                                existingAnswer.IsCorrect = incomingAnswer.IsCorrect;
                                existingAnswer.Name = incomingAnswer.Content.Length > 200 ? incomingAnswer.Content.Substring(0, 197) + "..." : incomingAnswer.Content;
                            }
                        }
                        else
                        {
                            existingEntity.QuestionAnswers.Add(new QuestionAnswer
                            {
                                Content = incomingAnswer.Content,
                                IsCorrect = incomingAnswer.IsCorrect,
                                Code = $"QA_{Guid.NewGuid().ToString().Substring(0, 8)}",
                                Name = incomingAnswer.Content.Length > 200 ? incomingAnswer.Content.Substring(0, 197) + "..." : incomingAnswer.Content
                            });
                        }
                    }
                }

                await _repository.UpdateAsync(existingEntity);
                await _repository.SaveChangesAsync();

                var savedEntity = await _repository.FindAll()
                    .Include(q => q.QuestionCategory)
                    .Include(q => q.QuestionAnswers)
                    .FirstOrDefaultAsync(q => q.Id == existingEntity.Id);

                return ApiResponse<QuestionDto>.Ok(MapToDto(savedEntity!), "UPDATE_QUESTION_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<QuestionDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<bool>> DeleteAsync(int id)
        {
            try
            {
                var exists = await _repository.ExistsAsync(q => q.Id == id);
                if (!exists)
                {
                    return ApiResponse<bool>.Fail("ERR_QUESTION_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                if (await _repository.IsUsedInExamAsync(id))
                {
                    return ApiResponse<bool>.Fail("ERR_QUESTION_IN_USE", StatusCodes.Status400BadRequest);
                }

                // Real removal from DB (not IsDeleted = true) — see IQuestionRepository.HardDeleteAsync.
                await _repository.HardDeleteAsync(id);

                return ApiResponse<bool>.Ok(true, "DELETE_QUESTION_SUCCESS");
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
                    return ApiResponse<bool>.Fail("ERR_QUESTION_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                await _repository.DeactiveAsync(existingEntity);
                await _repository.SaveChangesAsync();

                return ApiResponse<bool>.Ok(true, "DEACTIVATE_QUESTION_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        // ===================== PRIVATE MAPPING =====================

        private static QuestionDto MapToDto(Question entity) => new()
        {
            Id = entity.Id,
            Code = entity.Code ?? string.Empty,
            Name = entity.Name ?? string.Empty,
            Content = entity.Content ?? string.Empty,
            QuestionType = entity.QuestionType,
            QuestionTypeName = ((QuestionType)entity.QuestionType).GetStringValue(),
            SkillType = entity.SkillType,
            SkillTypeName = Enum.IsDefined(typeof(SkillType), entity.SkillType)
                ? ((SkillType)entity.SkillType).GetStringValue()
                : string.Empty,
            DifficultyLevel = entity.DifficultyLevel,
            DifficultyLevelName = ((DifficultyLevel)entity.DifficultyLevel).GetStringValue(),
            Explanation = entity.Explanation,
            MediaUrl = entity.MediaUrl,
            Status = entity.Status,
            CategoryId = entity.CategoryId,
            CategoryName = entity.QuestionCategory?.Name,
            Point = entity.Point,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            QuestionAnswers = entity.QuestionAnswers != null ? entity.QuestionAnswers
                .Where(a => !a.IsDeleted)
                .Select(a => new QuestionAnswerDto
                {
                    Id = a.Id,
                    Content = a.Content,
                    IsCorrect = a.IsCorrect
                }).ToList() : new List<QuestionAnswerDto>()
        };

        public async Task<ApiResponse<List<QuestionDto>>> ImportAsync(List<QuestionSaveDto> dtos)
        {
            var results = new List<QuestionDto>();
            var errors = new List<string>();

            foreach (var dto in dtos)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(dto.Code))
                    {
                        dto.Code = $"Q_{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";
                    }

                    var res = await CreateAsync(dto);
                    if (res.Success && res.Data != null)
                    {
                        results.Add(res.Data);
                    }
                    else
                    {
                        errors.Add($"Câu hỏi '{dto.Name}': {res.Message}");
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Câu hỏi '{dto.Name}': {ex.Message}");
                }
            }

            if (errors.Any())
            {
                var combinedMessage = string.Join("; ", errors);
                if (results.Any())
                {
                    return ApiResponse<List<QuestionDto>>.Ok(results, $"IMPORT_PARTIAL_SUCCESS: {combinedMessage}");
                }
                return ApiResponse<List<QuestionDto>>.Fail($"ERR_IMPORT_FAILED: {combinedMessage}", StatusCodes.Status400BadRequest);
            }

            return ApiResponse<List<QuestionDto>>.Ok(results, "IMPORT_QUESTION_SUCCESS");
        }

        // ===================== PRIVATE VALIDATE =====================

        private async Task<string?> ValidateAsync(QuestionSaveDto dto, bool isEdit)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return "ERR_TITLE_EMPTY";

            if (dto.Name.Length > 200)
                return "ERR_TITLE_MAX_LENGTH";

            if (string.IsNullOrWhiteSpace(dto.Content))
                return "ERR_CONTENT_EMPTY";

            if (!Enum.IsDefined(typeof(QuestionType), dto.QuestionType))
                return "ERR_INVALID_QUESTION_TYPE";

            if (!Enum.IsDefined(typeof(DifficultyLevel), dto.DifficultyLevel))
                return "ERR_INVALID_DIFFICULTY_LEVEL";

            if (dto.CategoryId.HasValue)
            {
                var category = await _categoryRepository.GetByIdAsync(dto.CategoryId.Value);
                if (category == null)
                    return "ERR_CATEGORY_NOT_FOUND";
            }

            if (dto.QuestionType != (int)QuestionType.Essay)
            {
                if (dto.QuestionAnswers == null || dto.QuestionAnswers.Count < 2)
                    return "ERR_MIN_TWO_ANSWERS_REQUIRED";

                if (dto.QuestionAnswers.Count > 6)
                    return "ERR_MAX_SIX_ANSWERS_ALLOWED";

                if (dto.QuestionAnswers.Any(a => string.IsNullOrWhiteSpace(a.Content)))
                    return "ERR_ANSWER_CONTENT_EMPTY";

                var correctCount = dto.QuestionAnswers.Count(a => a.IsCorrect);

                if (dto.QuestionType == (int)QuestionType.SingleChoice || dto.QuestionType == (int)QuestionType.TrueFalse)
                {
                    if (correctCount != 1)
                        return "ERR_MUST_HAVE_EXACTLY_ONE_CORRECT_ANSWER";
                }
                else if (dto.QuestionType == (int)QuestionType.MultipleChoice)
                {
                    if (correctCount < 1)
                        return "ERR_MUST_HAVE_AT_LEAST_ONE_CORRECT_ANSWER";
                }
            }

            // Check duplicate code if explicitly provided
            if (!string.IsNullOrWhiteSpace(dto.Code))
            {
                var duplicateCode = await _repository.FindAll()
                    .FirstOrDefaultAsync(q => q.Code == dto.Code && (!isEdit || q.Id != dto.Id));
                if (duplicateCode != null)
                    return "ERR_CODE_DUPLICATE";
            }

            return null;
        }
    }
}

