using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PRN232_be.DTO;
using PRN232_be.DTO.Exam;
using PRN232_be.DTO.Question;
using PRN232_be.Models;
using PRN232_be.Services.Interfaces;

namespace PRN232_be.Services.Implementations
{
    public class ExamService : IExamService
    {
        private readonly ApplicationDbContext _dbContext;

        public ExamService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<ApiResponse<PagingResponse<ExamDto>>> GetAllAsync(ExamSearchDto searchDto)
        {
            try
            {
                var query = _dbContext.Exams
                    .Include(e => e.Class)
                    .Include(e => e.ExamQuestions)
                    .Include(e => e.ExamAttempts)
                    .Where(e => !e.IsDeleted)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(searchDto.Keyword))
                {
                    query = query.Where(e => e.Title.Contains(searchDto.Keyword) 
                                             || (e.Code != null && e.Code.Contains(searchDto.Keyword))
                                             || (e.Description != null && e.Description.Contains(searchDto.Keyword)));
                }

                if (searchDto.ClassId.HasValue)
                {
                    query = query.Where(e => e.ClassId == searchDto.ClassId.Value);
                }

                if (searchDto.Status.HasValue)
                {
                    query = query.Where(e => e.Status == searchDto.Status.Value);
                }

                if (searchDto.Type.HasValue)
                {
                    query = query.Where(e => e.Type == searchDto.Type.Value);
                }

                var totalCount = await query.CountAsync();
                
                var items = await query
                    .OrderByDescending(e => e.Id)
                    .Skip((searchDto.PageNumber - 1) * searchDto.PageSize)
                    .Take(searchDto.PageSize)
                    .Select(e => new ExamDto
                    {
                        Id = e.Id,
                        Code = e.Code,
                        Name = e.Name,
                        Title = e.Title,
                        Description = e.Description,
                        ClassId = e.ClassId,
                        ClassName = e.Class != null ? e.Class.Name : null,
                        ScheduleId = e.ScheduleId,
                        Type = e.Type,
                        StartTime = e.StartTime,
                        EndTime = e.EndTime,
                        Duration = e.Duration,
                        TotalScore = e.TotalScore,
                        PassingScore = e.PassingScore,
                        MaxAttempts = e.MaxAttempts,
                        AllowLateSubmit = e.AllowLateSubmit,
                        ShuffleQuestion = e.ShuffleQuestion,
                        ShowAnswerAfter = e.ShowAnswerAfter,
                        Status = e.Status,
                        CreatedAt = e.CreatedAt,
                        QuestionCount = e.ExamQuestions.Count,
                        SubmissionCount = e.ExamAttempts.Count
                    })
                    .ToListAsync();

                var paging = new PagingResponse<ExamDto>
                {
                    Items = items,
                    TotalRecords = totalCount,
                    PageIndex = searchDto.PageNumber,
                    PageSize = searchDto.PageSize
                };

                return ApiResponse<PagingResponse<ExamDto>>.Ok(paging, "GET_EXAM_LIST_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<PagingResponse<ExamDto>>.Fail("Error retrieving exams: " + ex.Message);
            }
        }

        public async Task<ApiResponse<ExamDto>> GetByIdAsync(int id)
        {
            try
            {
                var exam = await _dbContext.Exams
                    .Include(e => e.Class)
                    .Include(e => e.ExamQuestions)
                        .ThenInclude(eq => eq.Question)
                            .ThenInclude(q => q.QuestionAnswers)
                    .Include(e => e.ExamAttempts)
                    .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);

                if (exam == null)
                {
                    return ApiResponse<ExamDto>.Fail("ERR_EXAM_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                var dto = new ExamDto
                {
                    Id = exam.Id,
                    Code = exam.Code,
                    Name = exam.Name,
                    Title = exam.Title,
                    Description = exam.Description,
                    ClassId = exam.ClassId,
                    ClassName = exam.Class != null ? exam.Class.Name : null,
                    ScheduleId = exam.ScheduleId,
                    Type = exam.Type,
                    StartTime = exam.StartTime,
                    EndTime = exam.EndTime,
                    Duration = exam.Duration,
                    TotalScore = exam.TotalScore,
                    PassingScore = exam.PassingScore,
                    MaxAttempts = exam.MaxAttempts,
                    AllowLateSubmit = exam.AllowLateSubmit,
                    ShuffleQuestion = exam.ShuffleQuestion,
                    ShowAnswerAfter = exam.ShowAnswerAfter,
                    Status = exam.Status,
                    CreatedAt = exam.CreatedAt,
                    QuestionCount = exam.ExamQuestions.Count,
                    SubmissionCount = exam.ExamAttempts.Count,
                    QuestionIds = exam.ExamQuestions.Select(eq => eq.QuestionId).ToList(),
                    Questions = exam.ExamQuestions
                        .Where(eq => eq.Question != null)
                        .Select(eq => new QuestionDto
                        {
                            Id = eq.Question.Id,
                            Code = eq.Question.Code,
                            Name = eq.Question.Name,
                            Content = eq.Question.Content,
                            QuestionType = eq.Question.QuestionType,
                            QuestionTypeName = eq.Question.QuestionType == 1 ? "Chọn một" :
                                               eq.Question.QuestionType == 2 ? "Chọn nhiều" :
                                               eq.Question.QuestionType == 3 ? "Nhập text" : "Đúng/Sai",
                            DifficultyLevel = eq.Question.DifficultyLevel,
                            DifficultyLevelName = eq.Question.DifficultyLevel == 1 ? "Dễ" :
                                                  eq.Question.DifficultyLevel == 2 ? "Trung bình" : "Khó",
                            Explanation = eq.Question.Explanation,
                            Status = eq.Question.Status,
                            CategoryId = eq.Question.CategoryId,
                            Point = eq.Point,
                            QuestionAnswers = eq.Question.QuestionAnswers.Select(qa => new QuestionAnswerDto
                            {
                                Id = qa.Id,
                                Content = qa.Content,
                                IsCorrect = qa.IsCorrect
                            }).ToList()
                        }).ToList()
                };

                return ApiResponse<ExamDto>.Ok(dto, "GET_EXAM_DETAIL_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<ExamDto>.Fail("Error retrieving exam: " + ex.Message);
            }
        }

        public async Task<ApiResponse<ExamDto>> CreateAsync(ExamSaveDto dto)
        {
            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                var code = "EX-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
                var name = dto.Title;

                var exam = new Exam
                {
                    Code = code,
                    Name = name,
                    Title = dto.Title,
                    Description = dto.Description,
                    ClassId = dto.ClassId,
                    ScheduleId = dto.ScheduleId,
                    Type = dto.Type,
                    StartTime = dto.StartTime,
                    EndTime = dto.EndTime,
                    Duration = dto.Duration,
                    TotalScore = dto.TotalScore,
                    PassingScore = dto.PassingScore,
                    MaxAttempts = dto.MaxAttempts,
                    AllowLateSubmit = dto.AllowLateSubmit,
                    ShuffleQuestion = dto.ShuffleQuestion,
                    ShowAnswerAfter = dto.ShowAnswerAfter,
                    Status = dto.Status,
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.Exams.Add(exam);
                await _dbContext.SaveChangesAsync();

                if (dto.QuestionIds != null && dto.QuestionIds.Count > 0)
                {
                    var defaultPoint = dto.TotalScore.HasValue ? dto.TotalScore.Value / dto.QuestionIds.Count : 1.0m;
                    foreach (var questionId in dto.QuestionIds)
                    {
                        var eq = new ExamQuestion
                        {
                            ExamId = exam.Id,
                            QuestionId = questionId,
                            Point = defaultPoint
                        };
                        _dbContext.ExamQuestions.Add(eq);
                    }
                    await _dbContext.SaveChangesAsync();
                }

                await transaction.CommitAsync();

                var result = await GetByIdAsync(exam.Id);
                if (result.Success && result.Data != null)
                {
                    return ApiResponse<ExamDto>.Created(result.Data, "CREATE_EXAM_SUCCESS");
                }
                return ApiResponse<ExamDto>.Fail("Error retrieving created exam details");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return ApiResponse<ExamDto>.Fail("Error creating exam: " + ex.Message);
            }
        }

        public async Task<ApiResponse<ExamDto>> EditAsync(ExamSaveDto dto)
        {
            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                var exam = await _dbContext.Exams
                    .Include(e => e.ExamQuestions)
                    .FirstOrDefaultAsync(e => e.Id == dto.Id && !e.IsDeleted);

                if (exam == null)
                {
                    return ApiResponse<ExamDto>.Fail("ERR_EXAM_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                exam.Title = dto.Title;
                exam.Name = dto.Title; // keep StandardEntity Name updated
                exam.Description = dto.Description;
                exam.ClassId = dto.ClassId;
                exam.ScheduleId = dto.ScheduleId;
                exam.Type = dto.Type;
                exam.StartTime = dto.StartTime;
                exam.EndTime = dto.EndTime;
                exam.Duration = dto.Duration;
                exam.TotalScore = dto.TotalScore;
                exam.PassingScore = dto.PassingScore;
                exam.MaxAttempts = dto.MaxAttempts;
                exam.AllowLateSubmit = dto.AllowLateSubmit;
                exam.ShuffleQuestion = dto.ShuffleQuestion;
                exam.ShowAnswerAfter = dto.ShowAnswerAfter;
                exam.Status = dto.Status;
                exam.UpdatedAt = DateTime.UtcNow;

                _dbContext.Exams.Update(exam);
                await _dbContext.SaveChangesAsync();

                // Clear old question relations
                var oldRelations = _dbContext.ExamQuestions.Where(eq => eq.ExamId == exam.Id);
                _dbContext.ExamQuestions.RemoveRange(oldRelations);
                await _dbContext.SaveChangesAsync();

                // Add new question relations
                if (dto.QuestionIds != null && dto.QuestionIds.Count > 0)
                {
                    var defaultPoint = dto.TotalScore.HasValue ? dto.TotalScore.Value / dto.QuestionIds.Count : 1.0m;
                    foreach (var questionId in dto.QuestionIds)
                    {
                        var eq = new ExamQuestion
                        {
                            ExamId = exam.Id,
                            QuestionId = questionId,
                            Point = defaultPoint
                        };
                        _dbContext.ExamQuestions.Add(eq);
                    }
                    await _dbContext.SaveChangesAsync();
                }

                await transaction.CommitAsync();

                var result = await GetByIdAsync(exam.Id);
                if (result.Success && result.Data != null)
                {
                    return ApiResponse<ExamDto>.Ok(result.Data, "UPDATE_EXAM_SUCCESS");
                }
                return ApiResponse<ExamDto>.Fail("Error retrieving updated exam details");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return ApiResponse<ExamDto>.Fail("Error editing exam: " + ex.Message);
            }
        }

        public async Task<ApiResponse<bool>> DeleteAsync(int id)
        {
            try
            {
                var exam = await _dbContext.Exams.FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);
                if (exam == null)
                {
                    return ApiResponse<bool>.Fail("ERR_EXAM_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                exam.IsDeleted = true;
                exam.DeletedAt = DateTime.UtcNow;
                _dbContext.Exams.Update(exam);
                await _dbContext.SaveChangesAsync();

                return ApiResponse<bool>.Ok(true, "DELETE_EXAM_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.Fail("Error deleting exam: " + ex.Message);
            }
        }

        public async Task<ApiResponse<ExamDto>> CopyAsync(int id)
        {
            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                var exam = await _dbContext.Exams
                    .Include(e => e.ExamQuestions)
                    .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);

                if (exam == null)
                {
                    return ApiResponse<ExamDto>.Fail("ERR_EXAM_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                var code = "EX-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
                var copiedExam = new Exam
                {
                    Code = code,
                    Name = "[Copy] " + exam.Title,
                    Title = "[Copy] " + exam.Title,
                    Description = exam.Description,
                    ClassId = exam.ClassId,
                    ScheduleId = exam.ScheduleId,
                    Type = exam.Type,
                    StartTime = exam.StartTime,
                    EndTime = exam.EndTime,
                    Duration = exam.Duration,
                    TotalScore = exam.TotalScore,
                    PassingScore = exam.PassingScore,
                    MaxAttempts = exam.MaxAttempts,
                    AllowLateSubmit = exam.AllowLateSubmit,
                    ShuffleQuestion = exam.ShuffleQuestion,
                    ShowAnswerAfter = exam.ShowAnswerAfter,
                    Status = 2, // Always copy as Draft
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.Exams.Add(copiedExam);
                await _dbContext.SaveChangesAsync();

                foreach (var eq in exam.ExamQuestions)
                {
                    var copiedEq = new ExamQuestion
                    {
                        ExamId = copiedExam.Id,
                        QuestionId = eq.QuestionId,
                        Point = eq.Point
                    };
                    _dbContext.ExamQuestions.Add(copiedEq);
                }
                await _dbContext.SaveChangesAsync();

                await transaction.CommitAsync();

                var result = await GetByIdAsync(copiedExam.Id);
                if (result.Success && result.Data != null)
                {
                    return ApiResponse<ExamDto>.Ok(result.Data, "COPY_EXAM_SUCCESS");
                }
                return ApiResponse<ExamDto>.Fail("Error retrieving copied exam details");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return ApiResponse<ExamDto>.Fail("Error copying exam: " + ex.Message);
            }
        }
    }
}
