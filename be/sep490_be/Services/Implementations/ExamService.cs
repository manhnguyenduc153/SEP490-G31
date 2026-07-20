using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using sep490_be.DTO;
using sep490_be.DTO.Exam;
using sep490_be.DTO.Question;
using sep490_be.Enums;
using sep490_be.Models;
using sep490_be.Services.Interfaces;

namespace sep490_be.Services.Implementations
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

        public async Task<ApiResponse<List<ExamAttemptDto>>> GetStudentAttemptsAsync(int examId, string userEmailOrCode)
        {
            try
            {
                var student = await GetStudentByIdentifierAsync(userEmailOrCode);
                if (student == null)
                {
                    return ApiResponse<List<ExamAttemptDto>>.Fail("Học sinh không tồn tại.");
                }

                var exam = await _dbContext.Exams.FirstOrDefaultAsync(e => e.Id == examId && !e.IsDeleted);
                if (exam == null)
                {
                    return ApiResponse<List<ExamAttemptDto>>.Fail("Bài kiểm tra không tồn tại.");
                }

                var attempts = await _dbContext.ExamAttempts
                    .Include(a => a.ExamAnswers)
                    .Where(a => a.ExamId == examId && a.StudentId == student.Id && !a.IsDeleted)
                    .OrderBy(a => a.StartTime)
                    .Select(a => new ExamAttemptDto
                    {
                        Id = a.Id,
                        ExamId = a.ExamId,
                        ExamTitle = exam.Title,
                        StudentId = a.StudentId,
                        StudentName = student.Name,
                        StudentCode = student.Code ?? string.Empty,
                        StartTime = a.StartTime,
                        SubmitTime = a.SubmitTime,
                        Score = a.Score,
                        Status = a.Status,
                        Duration = exam.Duration,
                        Answers = a.ExamAnswers.Select(ans => new ExamAnswerDto
                        {
                            Id = ans.Id,
                            QuestionId = ans.QuestionId,
                            AnswerContent = ans.AnswerContent,
                            AttachmentUrl = ans.AttachmentUrl,
                            Score = ans.Score,
                            TeacherComment = ans.TeacherComment
                        }).ToList()
                    })
                    .ToListAsync();

                // Fetch correct status for each answer
                var examDetailRes = await GetByIdAsync(examId);
                if (examDetailRes.Success && examDetailRes.Data != null)
                {
                    var questionsMap = examDetailRes.Data.Questions.ToDictionary(q => q.Id);
                    foreach (var attempt in attempts)
                    {
                        foreach (var ans in attempt.Answers)
                        {
                            if (questionsMap.TryGetValue(ans.QuestionId, out var qDto))
                            {
                                // Check correctness
                                var correctAnswers = qDto.QuestionAnswers.Where(qa => qa.IsCorrect).Select(qa => qa.Content.Trim().ToLower()).ToList();
                                var correctIds = qDto.QuestionAnswers.Where(qa => qa.IsCorrect).Select(qa => qa.Id.ToString()).ToList();
                                
                                if (!string.IsNullOrEmpty(ans.AnswerContent))
                                {
                                    var stdAns = ans.AnswerContent.Trim().ToLower();
                                    if (qDto.QuestionType == 1 || qDto.QuestionType == 4)
                                    {
                                        ans.IsCorrect = correctAnswers.Contains(stdAns) || correctIds.Contains(stdAns);
                                    }
                                    else if (qDto.QuestionType == 2)
                                    {
                                        var stdAnswers = stdAns.Split(',').Select(s => s.Trim()).ToHashSet();
                                        var correctSet = correctAnswers.ToHashSet();
                                        var correctIdSet = correctIds.ToHashSet();
                                        ans.IsCorrect = stdAnswers.SetEquals(correctSet) || stdAnswers.SetEquals(correctIdSet);
                                    }
                                }
                            }
                        }
                    }
                }

                return ApiResponse<List<ExamAttemptDto>>.Ok(attempts, "GET_ATTEMPTS_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<List<ExamAttemptDto>>.Fail("Error: " + ex.Message);
            }
        }

        public async Task<ApiResponse<List<ExamAttemptDto>>> GetAttemptsByExamAsync(int examId)
        {
            try
            {
                var exam = await _dbContext.Exams.FirstOrDefaultAsync(e => e.Id == examId && !e.IsDeleted);
                if (exam == null)
                {
                    return ApiResponse<List<ExamAttemptDto>>.Fail("Bai kiem tra khong ton tai.");
                }

                var attempts = await _dbContext.ExamAttempts
                    .AsNoTracking()
                    .Include(a => a.Student)
                    .Include(a => a.ExamAnswers)
                    .Where(a => a.ExamId == examId && !a.IsDeleted)
                    .OrderBy(a => a.Student.Name)
                    .ThenByDescending(a => a.SubmitTime ?? a.StartTime)
                    .Select(a => new ExamAttemptDto
                    {
                        Id = a.Id,
                        ExamId = a.ExamId,
                        ExamTitle = exam.Title,
                        StudentId = a.StudentId,
                        StudentName = a.Student.Name,
                        StudentCode = a.Student.Code ?? string.Empty,
                        StartTime = a.StartTime,
                        SubmitTime = a.SubmitTime,
                        Score = a.Score,
                        Status = a.Status,
                        Duration = exam.Duration,
                        Answers = a.ExamAnswers.Select(ans => new ExamAnswerDto
                        {
                            Id = ans.Id,
                            QuestionId = ans.QuestionId,
                            AnswerContent = ans.AnswerContent,
                            AttachmentUrl = ans.AttachmentUrl,
                            Score = ans.Score,
                            TeacherComment = ans.TeacherComment
                        }).ToList()
                    })
                    .ToListAsync();

                // Fetch correct status for each answer
                var examDetailRes = await GetByIdAsync(examId);
                if (examDetailRes.Success && examDetailRes.Data != null)
                {
                    var questionsMap = examDetailRes.Data.Questions.ToDictionary(q => q.Id);
                    foreach (var attempt in attempts)
                    {
                        foreach (var ans in attempt.Answers)
                        {
                            if (questionsMap.TryGetValue(ans.QuestionId, out var qDto))
                            {
                                // Check correctness
                                var correctAnswers = qDto.QuestionAnswers.Where(qa => qa.IsCorrect).Select(qa => qa.Content.Trim().ToLower()).ToList();
                                var correctIds = qDto.QuestionAnswers.Where(qa => qa.IsCorrect).Select(qa => qa.Id.ToString()).ToList();
                                
                                if (!string.IsNullOrEmpty(ans.AnswerContent))
                                {
                                    var stdAns = ans.AnswerContent.Trim().ToLower();
                                    if (qDto.QuestionType == 1 || qDto.QuestionType == 4)
                                    {
                                        ans.IsCorrect = correctAnswers.Contains(stdAns) || correctIds.Contains(stdAns);
                                    }
                                    else if (qDto.QuestionType == 2)
                                    {
                                        var stdAnswers = stdAns.Split(',').Select(s => s.Trim()).ToHashSet();
                                        var correctSet = correctAnswers.ToHashSet();
                                        var correctIdSet = correctIds.ToHashSet();
                                        ans.IsCorrect = stdAnswers.SetEquals(correctSet) || stdAnswers.SetEquals(correctIdSet);
                                    }
                                }
                            }
                        }
                    }
                }

                return ApiResponse<List<ExamAttemptDto>>.Ok(attempts, "GET_ATTEMPTS_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<List<ExamAttemptDto>>.Fail("Error: " + ex.Message);
            }
        }

        public async Task<ApiResponse<ExamAttemptDto>> StartAttemptAsync(int examId, string userEmailOrCode)
        {
            try
            {
                var student = await GetStudentByIdentifierAsync(userEmailOrCode);
                if (student == null)
                {
                    return ApiResponse<ExamAttemptDto>.Fail("Học sinh không tồn tại.");
                }

                var exam = await _dbContext.Exams
                    .Include(e => e.ExamAttempts)
                    .FirstOrDefaultAsync(e => e.Id == examId && !e.IsDeleted);
                if (exam == null)
                {
                    return ApiResponse<ExamAttemptDto>.Fail("Bài kiểm tra không tồn tại.");
                }

                if (exam.Status != 1)
                {
                    return ApiResponse<ExamAttemptDto>.Fail("Bài kiểm tra chưa được xuất bản.");
                }

                var existingAttempts = exam.ExamAttempts.Where(a => a.StudentId == student.Id && !a.IsDeleted).ToList();
                if (exam.MaxAttempts.HasValue && existingAttempts.Count >= exam.MaxAttempts.Value)
                {
                    return ApiResponse<ExamAttemptDto>.Fail("Bạn đã vượt quá số lần làm bài cho phép.");
                }

                // Check if there is already an In Progress attempt
                var inProgressAttempt = existingAttempts.FirstOrDefault(a => a.Status == 1);
                if (inProgressAttempt != null)
                {
                    // Return the existing in progress attempt
                    return ApiResponse<ExamAttemptDto>.Ok(new ExamAttemptDto
                    {
                        Id = inProgressAttempt.Id,
                        ExamId = inProgressAttempt.ExamId,
                        ExamTitle = exam.Title,
                        StudentId = inProgressAttempt.StudentId,
                        StudentName = student.Name,
                        StudentCode = student.Code ?? string.Empty,
                        StartTime = inProgressAttempt.StartTime,
                        SubmitTime = inProgressAttempt.SubmitTime,
                        Score = inProgressAttempt.Score,
                        Status = inProgressAttempt.Status,
                        Duration = exam.Duration
                    }, "EXAM_ATTEMPT_CONTINUE");
                }

                var attempt = new ExamAttempt
                {
                    Code = "ATT-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper(),
                    Name = $"Lượt làm bài của {student.Name} cho {exam.Title}",
                    ExamId = examId,
                    StudentId = student.Id,
                    StartTime = DateTime.Now,
                    Status = 1 // In Progress
                };

                _dbContext.ExamAttempts.Add(attempt);
                await _dbContext.SaveChangesAsync();

                return ApiResponse<ExamAttemptDto>.Ok(new ExamAttemptDto
                {
                    Id = attempt.Id,
                    ExamId = attempt.ExamId,
                    ExamTitle = exam.Title,
                    StudentId = attempt.StudentId,
                    StudentName = student.Name,
                    StudentCode = student.Code ?? string.Empty,
                    StartTime = attempt.StartTime,
                    Status = attempt.Status,
                    Duration = exam.Duration
                }, "EXAM_ATTEMPT_STARTED");
            }
            catch (Exception ex)
            {
                return ApiResponse<ExamAttemptDto>.Fail("Error starting exam attempt: " + ex.Message);
            }
        }

        public async Task<ApiResponse<ExamAttemptDto>> SubmitAttemptAsync(int examId, ExamSubmitDto submitDto, string userEmailOrCode)
        {
            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                var student = await GetStudentByIdentifierAsync(userEmailOrCode);
                if (student == null)
                {
                    return ApiResponse<ExamAttemptDto>.Fail("Học sinh không tồn tại.");
                }

                var exam = await _dbContext.Exams
                    .Include(e => e.ExamQuestions)
                        .ThenInclude(eq => eq.Question)
                            .ThenInclude(q => q.QuestionAnswers)
                    .FirstOrDefaultAsync(e => e.Id == examId && !e.IsDeleted);

                if (exam == null)
                {
                    return ApiResponse<ExamAttemptDto>.Fail("Bài kiểm tra không tồn tại.");
                }

                var attempt = await _dbContext.ExamAttempts
                    .FirstOrDefaultAsync(a => a.Id == submitDto.AttemptId && a.ExamId == examId && a.StudentId == student.Id && !a.IsDeleted);

                if (attempt == null)
                {
                    return ApiResponse<ExamAttemptDto>.Fail("Không tìm thấy lượt làm bài tương ứng.");
                }

                if (attempt.Status == 2)
                {
                    return ApiResponse<ExamAttemptDto>.Fail("Bài làm này đã được nộp trước đó.");
                }

                attempt.SubmitTime = DateTime.Now;
                attempt.Status = 2; // Submitted

                decimal totalScore = 0;
                var listAnswers = new List<ExamAnswerDto>();

                // Load existing answers if any (to update/overwrite)
                var existingAnswers = await _dbContext.ExamAnswers.Where(ea => ea.ExamAttemptId == attempt.Id).ToListAsync();
                _dbContext.ExamAnswers.RemoveRange(existingAnswers);

                var eqMap = exam.ExamQuestions.ToDictionary(eq => eq.QuestionId);

                foreach (var ansDto in submitDto.Answers)
                {
                    if (!eqMap.TryGetValue(ansDto.QuestionId, out var eq)) continue;
                    var q = eq.Question;
                    if (q == null) continue;

                    decimal questionScore = 0;
                    bool isCorrect = false;

                    var correctAnswers = q.QuestionAnswers.Where(qa => qa.IsCorrect).Select(qa => qa.Content.Trim().ToLower()).ToList();
                    var correctIds = q.QuestionAnswers.Where(qa => qa.IsCorrect).Select(qa => qa.Id.ToString()).ToList();

                    if (!string.IsNullOrEmpty(ansDto.AnswerContent))
                    {
                        var stdAns = ansDto.AnswerContent.Trim().ToLower();
                        if (q.QuestionType == 1 || q.QuestionType == 4) // Single choice / True-False
                        {
                            isCorrect = correctAnswers.Contains(stdAns) || correctIds.Contains(stdAns);
                        }
                        else if (q.QuestionType == 2) // Multiple Choice
                        {
                            var stdAnswers = stdAns.Split(',').Select(s => s.Trim()).ToHashSet();
                            var correctSet = correctAnswers.ToHashSet();
                            var correctIdSet = correctIds.ToHashSet();
                            isCorrect = stdAnswers.SetEquals(correctSet) || stdAnswers.SetEquals(correctIdSet);
                        }
                    }

                    if (isCorrect)
                    {
                        questionScore = eq.Point;
                        totalScore += questionScore;
                    }

                    var newAns = new ExamAnswer
                    {
                        Code = "ANS-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper(),
                        Name = $"Câu trả lời của {student.Name} cho câu hỏi {q.Code}",
                        ExamAttemptId = attempt.Id,
                        QuestionId = ansDto.QuestionId,
                        AnswerContent = ansDto.AnswerContent,
                        Score = questionScore
                    };

                    _dbContext.ExamAnswers.Add(newAns);
                    listAnswers.Add(new ExamAnswerDto
                    {
                        QuestionId = ansDto.QuestionId,
                        AnswerContent = ansDto.AnswerContent,
                        Score = questionScore,
                        IsCorrect = isCorrect
                    });
                }

                attempt.Score = totalScore;
                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                return ApiResponse<ExamAttemptDto>.Ok(new ExamAttemptDto
                {
                    Id = attempt.Id,
                    ExamId = attempt.ExamId,
                    ExamTitle = exam.Title,
                    StudentId = attempt.StudentId,
                    StudentName = student.Name,
                    StudentCode = student.Code ?? string.Empty,
                    StartTime = attempt.StartTime,
                    SubmitTime = attempt.SubmitTime,
                    Score = attempt.Score,
                    Status = attempt.Status,
                    Duration = exam.Duration,
                    Answers = listAnswers
                }, "SUBMIT_EXAM_SUCCESS");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return ApiResponse<ExamAttemptDto>.Fail("Error submitting exam: " + ex.Message);
            }
        }

        public async Task<ApiResponse<List<ExamDto>>> GetStudentExamsAsync(string userEmailOrCode)
        {
            try
            {
                // Find student by email or code — same pattern as StartAttemptAsync
                var student = await GetStudentByIdentifierAsync(userEmailOrCode);

                if (student == null)
                {
                    return ApiResponse<List<ExamDto>>.Fail("ERR_STUDENT_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                // Get student's enrolled/studying class ids — Enrolled=0, Studying=1
                var classIds = student.StudentClasses
                    .Where(sc => sc.Status == (int)Enums.StudentClassStatus.Enrolled
                              || sc.Status == (int)Enums.StudentClassStatus.Studying)
                    .Select(sc => sc.ClassId)
                    .ToList();

                if (!classIds.Any())
                {
                    return ApiResponse<List<ExamDto>>.Ok(new List<ExamDto>(), "GET_STUDENT_EXAMS_SUCCESS");
                }

                // Get published exams assigned to these classes
                var exams = await _dbContext.Exams
                    .Include(e => e.Class)
                    .Include(e => e.ExamAttempts)
                    .Where(e => e.ClassId.HasValue && classIds.Contains(e.ClassId.Value) && e.Status == 1 && !e.IsDeleted)
                    .OrderByDescending(e => e.Id)
                    .ToListAsync();

                // Map to ExamDto
                var dtos = exams.Select(e => new ExamDto
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
                    QuestionCount = _dbContext.ExamQuestions.Count(eq => eq.ExamId == e.Id),
                    SubmissionCount = e.ExamAttempts.Count(ea => ea.StudentId == student.Id && ea.Status == 2)
                }).ToList();

                return ApiResponse<List<ExamDto>>.Ok(dtos, "GET_STUDENT_EXAMS_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<List<ExamDto>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        private async Task<Student?> GetStudentByIdentifierAsync(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier)) return null;

            // 1. Try finding by Email or Code directly
            var student = await _dbContext.Students
                .Include(s => s.StudentClasses)
                .FirstOrDefaultAsync(s => (s.Email == identifier || s.Code == identifier) && !s.IsDeleted);

            if (student != null) return student;

            // 2. Try looking up Identity User by UserName, Email, or Id
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => 
                u.UserName == identifier || 
                u.Email == identifier || 
                u.Id == identifier);

            if (user != null)
            {
                student = await _dbContext.Students
                    .Include(s => s.StudentClasses)
                    .FirstOrDefaultAsync(s => (s.Email == user.Email || s.Email == user.UserName || s.Code == user.UserName) && !s.IsDeleted);
            }

            return student;
        }
    }
}

