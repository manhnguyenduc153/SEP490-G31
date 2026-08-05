using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using sep490_be.DTO;
using sep490_be.DTO.Question;
using sep490_be.DTO.QuestionPassage;
using sep490_be.Enums;
using sep490_be.Helpers;
using sep490_be.Models;
using sep490_be.Repositories.Interfaces;
using sep490_be.Services.Interfaces;

namespace sep490_be.Services.Implementations
{
    public class QuestionPassageService : IQuestionPassageService
    {
        private readonly IQuestionPassageRepository _repository;
        private readonly IQuestionRepository _questionRepository;
        private readonly ApplicationDbContext _dbContext;

        public QuestionPassageService(
            IQuestionPassageRepository repository,
            IQuestionRepository questionRepository,
            ApplicationDbContext dbContext)
        {
            _repository = repository;
            _questionRepository = questionRepository;
            _dbContext = dbContext;
        }

        public async Task<ApiResponse<PagingResponse<QuestionPassageDto>>> GetAllAsync(QuestionPassageSearchDto searchDto)
        {
            try
            {
                var query = _repository.FindAll()
                    .Include(p => p.QuestionCategory)
                    .Include(p => p.Questions)
                        .ThenInclude(q => q.QuestionAnswers)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(searchDto.Keyword))
                {
                    query = query.Where(p => p.TextSearch != null && p.TextSearch.Contains(searchDto.Keyword));
                }

                if (searchDto.SkillType.HasValue)
                {
                    query = query.Where(p => p.SkillType == searchDto.SkillType.Value);
                }

                if (searchDto.CategoryId.HasValue)
                {
                    query = query.Where(p => p.CategoryId == searchDto.CategoryId.Value);
                }

                query = query.OrderByDescending(p => p.Id);

                var totalRecords = await query.CountAsync();
                var entities = await query.ApplyPagingAsync(searchDto);

                var dtos = entities.Select(MapToDto).ToList();
                var pagingResponse = dtos.ToPagingResponse(totalRecords, searchDto);

                return ApiResponse<PagingResponse<QuestionPassageDto>>.Ok(pagingResponse, "GET_PASSAGE_LIST_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<PagingResponse<QuestionPassageDto>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<QuestionPassageDto>> GetByIdAsync(int id)
        {
            try
            {
                var entity = await _repository.FindAll()
                    .Include(p => p.QuestionCategory)
                    .Include(p => p.Questions)
                        .ThenInclude(q => q.QuestionAnswers)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (entity == null)
                {
                    return ApiResponse<QuestionPassageDto>.Fail("ERR_PASSAGE_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                return ApiResponse<QuestionPassageDto>.Ok(MapToDto(entity), "GET_PASSAGE_DETAIL_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<QuestionPassageDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<QuestionPassageDto>> CreateAsync(QuestionPassageSaveDto dto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dto.Title))
                    return ApiResponse<QuestionPassageDto>.Fail("ERR_TITLE_EMPTY", StatusCodes.Status400BadRequest);

                var entity = new QuestionPassage
                {
                    Id = 0,
                    Code = string.IsNullOrWhiteSpace(dto.Code) ? $"QP_{Guid.NewGuid().ToString().Substring(0, 8)}" : dto.Code,
                    Title = dto.Title.Trim(),
                    Content = dto.Content,
                    AudioUrl = dto.AudioUrl,
                    AttachmentUrl = dto.AttachmentUrl,
                    SkillType = dto.SkillType > 0 ? dto.SkillType : 1,
                    CategoryId = dto.CategoryId
                };

                if (dto.Questions != null && dto.Questions.Count > 0)
                {
                    foreach (var qDto in dto.Questions)
                    {
                        var qEntity = new Question
                        {
                            Code = string.IsNullOrWhiteSpace(qDto.Code) ? $"Q_{Guid.NewGuid().ToString().Substring(0, 8)}" : qDto.Code,
                            Name = string.IsNullOrWhiteSpace(qDto.Name) ? qDto.Content : qDto.Name,
                            Content = qDto.Content,
                            QuestionType = qDto.QuestionType > 0 ? qDto.QuestionType : 1,
                            SkillType = entity.SkillType,
                            DifficultyLevel = qDto.DifficultyLevel > 0 ? qDto.DifficultyLevel : 1,
                            Explanation = qDto.Explanation,
                            MediaUrl = qDto.MediaUrl,
                            Point = qDto.Point ?? 1.0m,
                            CategoryId = dto.CategoryId,
                            Status = 1
                        };

                        if (qDto.Answers != null && qDto.Answers.Count > 0)
                        {
                            foreach (var aDto in qDto.Answers)
                            {
                                qEntity.QuestionAnswers.Add(new QuestionAnswer
                                {
                                    Content = aDto.Content,
                                    IsCorrect = aDto.IsCorrect
                                });
                            }
                        }

                        entity.Questions.Add(qEntity);
                    }
                }

                await _repository.AddAsync(entity);
                await _repository.SaveChangesAsync();

                return await GetByIdAsync(entity.Id);
            }
            catch (Exception ex)
            {
                return ApiResponse<QuestionPassageDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<QuestionPassageDto>> EditAsync(QuestionPassageSaveDto dto)
        {
            try
            {
                var existingEntity = await _repository.FindAll()
                    .Include(p => p.Questions)
                        .ThenInclude(q => q.QuestionAnswers)
                    .FirstOrDefaultAsync(p => p.Id == dto.Id);

                if (existingEntity == null)
                {
                    return ApiResponse<QuestionPassageDto>.Fail("ERR_PASSAGE_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                if (string.IsNullOrWhiteSpace(dto.Title))
                    return ApiResponse<QuestionPassageDto>.Fail("ERR_TITLE_EMPTY", StatusCodes.Status400BadRequest);

                existingEntity.Title = dto.Title.Trim();
                existingEntity.Content = dto.Content;
                existingEntity.AudioUrl = dto.AudioUrl;
                existingEntity.AttachmentUrl = dto.AttachmentUrl;
                if (dto.SkillType > 0) existingEntity.SkillType = dto.SkillType;
                existingEntity.CategoryId = dto.CategoryId;

                // Update Child Questions
                if (dto.Questions != null)
                {
                    // Remove missing questions
                    var incomingIds = dto.Questions.Where(q => q.Id > 0).Select(q => q.Id).ToList();
                    var toDelete = existingEntity.Questions.Where(q => !incomingIds.Contains(q.Id)).ToList();
                    foreach (var q in toDelete)
                    {
                        existingEntity.Questions.Remove(q);
                    }

                    foreach (var qDto in dto.Questions)
                    {
                        if (qDto.Id > 0)
                        {
                            var existingQ = existingEntity.Questions.FirstOrDefault(q => q.Id == qDto.Id);
                            if (existingQ != null)
                            {
                                existingQ.Content = qDto.Content;
                                existingQ.QuestionType = qDto.QuestionType;
                                existingQ.DifficultyLevel = qDto.DifficultyLevel;
                                existingQ.Explanation = qDto.Explanation;
                                existingQ.MediaUrl = qDto.MediaUrl;
                                existingQ.Point = qDto.Point;
                                existingQ.CategoryId = dto.CategoryId;

                                existingQ.QuestionAnswers.Clear();
                                if (qDto.Answers != null)
                                {
                                    foreach (var aDto in qDto.Answers)
                                    {
                                        existingQ.QuestionAnswers.Add(new QuestionAnswer
                                        {
                                            Content = aDto.Content,
                                            IsCorrect = aDto.IsCorrect
                                        });
                                    }
                                }
                            }
                        }
                        else
                        {
                            var newQ = new Question
                            {
                                Code = string.IsNullOrWhiteSpace(qDto.Code) ? $"Q_{Guid.NewGuid().ToString().Substring(0, 8)}" : qDto.Code,
                                Name = string.IsNullOrWhiteSpace(qDto.Name) ? qDto.Content : qDto.Name,
                                Content = qDto.Content,
                                QuestionType = qDto.QuestionType > 0 ? qDto.QuestionType : 1,
                                SkillType = existingEntity.SkillType,
                                DifficultyLevel = qDto.DifficultyLevel > 0 ? qDto.DifficultyLevel : 1,
                                Explanation = qDto.Explanation,
                                MediaUrl = qDto.MediaUrl,
                                Point = qDto.Point ?? 1.0m,
                                CategoryId = dto.CategoryId,
                                Status = 1
                            };

                            if (qDto.Answers != null)
                            {
                                foreach (var aDto in qDto.Answers)
                                {
                                    newQ.QuestionAnswers.Add(new QuestionAnswer
                                    {
                                        Content = aDto.Content,
                                        IsCorrect = aDto.IsCorrect
                                    });
                                }
                            }

                            existingEntity.Questions.Add(newQ);
                        }
                    }
                }

                await _repository.UpdateAsync(existingEntity);
                await _repository.SaveChangesAsync();

                return await GetByIdAsync(existingEntity.Id);
            }
            catch (Exception ex)
            {
                return ApiResponse<QuestionPassageDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<bool>> DeleteAsync(int id)
        {
            try
            {
                var passage = await _repository.FindAll()
                    .Include(p => p.Questions)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (passage == null)
                {
                    return ApiResponse<bool>.Fail("ERR_PASSAGE_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                // Collect all child question IDs
                var childQuestionIds = passage.Questions.Select(q => q.Id).ToList();
                var standaloneQuestionIds = await _dbContext.Questions
                    .Where(q => q.PassageId == id || (q.Code != null && passage.Code != null && q.Code.StartsWith(passage.Code)))
                    .Select(q => q.Id)
                    .ToListAsync();

                var allQuestionIds = childQuestionIds.Union(standaloneQuestionIds).Distinct().ToList();

                // 1. Check if being used in any Exam
                var isUsedInExam = await _dbContext.ExamQuestions
                    .AnyAsync(eq => (eq.PassageId.HasValue && eq.PassageId.Value == id) || allQuestionIds.Contains(eq.QuestionId));

                if (!isUsedInExam)
                {
                    isUsedInExam = await _dbContext.ExamAnswers
                        .AnyAsync(ea => allQuestionIds.Contains(ea.QuestionId));
                }

                if (isUsedInExam)
                {
                    return ApiResponse<bool>.Fail("ERR_PASSAGE_IN_USE", StatusCodes.Status400BadRequest);
                }

                // 2. Hard Delete (Xóa cứng)
                var questionsToDelete = await _dbContext.Questions
                    .Where(q => q.PassageId == id || allQuestionIds.Contains(q.Id))
                    .Include(q => q.QuestionAnswers)
                    .ToListAsync();

                if (questionsToDelete.Count > 0)
                {
                    foreach (var q in questionsToDelete)
                    {
                        if (q.QuestionAnswers.Count > 0)
                        {
                            _dbContext.QuestionAnswers.RemoveRange(q.QuestionAnswers);
                        }
                    }
                    _dbContext.Questions.RemoveRange(questionsToDelete);
                }

                _dbContext.QuestionPassages.Remove(passage);
                await _dbContext.SaveChangesAsync();

                return ApiResponse<bool>.Ok(true, "DELETE_PASSAGE_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        private static QuestionPassageDto MapToDto(QuestionPassage entity) => new()
        {
            Id = entity.Id,
            Code = entity.Code ?? string.Empty,
            Title = entity.Title ?? string.Empty,
            Content = entity.Content,
            AudioUrl = entity.AudioUrl,
            AttachmentUrl = entity.AttachmentUrl,
            SkillType = entity.SkillType,
            SkillTypeName = Enum.IsDefined(typeof(SkillType), entity.SkillType)
                ? ((SkillType)entity.SkillType).GetStringValue()
                : string.Empty,
            CategoryId = entity.CategoryId,
            CategoryName = entity.QuestionCategory?.Name,
            Questions = entity.Questions?.Select(q => new QuestionDto
            {
                Id = q.Id,
                Code = q.Code ?? string.Empty,
                Name = q.Name ?? string.Empty,
                Content = q.Content ?? string.Empty,
                QuestionType = q.QuestionType,
                SkillType = q.SkillType,
                DifficultyLevel = q.DifficultyLevel,
                Explanation = q.Explanation,
                MediaUrl = q.MediaUrl,
                Point = q.Point,
                CategoryId = q.CategoryId,
                PassageId = q.PassageId,
                Answers = q.QuestionAnswers?.Select(a => new QuestionAnswerDto
                {
                    Id = a.Id,
                    Content = a.Content ?? string.Empty,
                    IsCorrect = a.IsCorrect
                }).ToList() ?? new List<QuestionAnswerDto>()
            }).ToList() ?? new List<QuestionDto>()
        };
    }
}
