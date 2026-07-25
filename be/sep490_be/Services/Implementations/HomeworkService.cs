using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using sep490_be.DTO.Homework;
using sep490_be.DTO;
using sep490_be.Helpers;
using sep490_be.Models;
using sep490_be.Repositories.Interfaces;
using sep490_be.Services.Interfaces;

namespace sep490_be.Services.Implementations
{
    public class HomeworkService : IHomeworkService
    {
        private readonly IHomeworkRepository _homeworkRepository;
        private readonly IHomeworkSubmissionRepository _homeworkSubmissionRepository;
        private readonly ApplicationDbContext _dbContext;

        public HomeworkService(
            IHomeworkRepository homeworkRepository,
            IHomeworkSubmissionRepository homeworkSubmissionRepository,
            ApplicationDbContext dbContext)
        {
            _homeworkRepository = homeworkRepository;
            _homeworkSubmissionRepository = homeworkSubmissionRepository;
            _dbContext = dbContext;
        }

        public async Task<ApiResponse<IEnumerable<HomeworkDto>>> GetHomeworkByClassAsync(int classId)
        {
            if (classId <= 0)
            {
                return ApiResponse<IEnumerable<HomeworkDto>>.Fail("ERR_HOMEWORK_CLASS_REQUIRED", StatusCodes.Status400BadRequest);
            }

            var classExists = await _dbContext.Classes
                .AsNoTracking()
                .AnyAsync(c => c.Id == classId && !c.IsDeleted);
            if (!classExists)
            {
                return ApiResponse<IEnumerable<HomeworkDto>>.Fail("ERR_HOMEWORK_CLASS_NOT_FOUND", StatusCodes.Status404NotFound);
            }

            var homeworks = await _homeworkRepository.FindByCondition(h => h.ClassId == classId && !h.IsDeleted)
                .Include(h => h.Teacher)
                .Include(h => h.Class)
                .OrderByDescending(h => h.CreatedAt)
                .Select(h => new HomeworkDto
                {
                    Id = h.Id,
                    ClassId = h.ClassId,
                    TeacherId = h.TeacherId,
                    Title = h.Title,
                    Description = h.Description,
                    AttachmentUrls = h.AttachmentUrls,
                    Skill = h.Skill,
                    DueDate = h.DueDate,
                    TotalScore = h.TotalScore,
                    Status = h.Status,
                    CreatedAt = h.CreatedAt,
                    CreatedBy = h.CreatedBy,
                    TeacherName = h.Teacher != null ? h.Teacher.Name : null,
                    ClassName = h.Class != null ? h.Class.Name : null
                })
                .ToListAsync();

            return ApiResponse<IEnumerable<HomeworkDto>>.Ok(homeworks);
        }

        public async Task<ApiResponse<IEnumerable<HomeworkDto>>> GetStudentHomeworkByClassAsync(int classId)
        {
            var homeworks = await _homeworkRepository.FindByCondition(h => h.ClassId == classId && !h.IsDeleted && h.Status == 1)
                .Include(h => h.Teacher)
                .Include(h => h.Class)
                .OrderByDescending(h => h.CreatedAt)
                .Select(h => new HomeworkDto
                {
                    Id = h.Id,
                    ClassId = h.ClassId,
                    TeacherId = h.TeacherId,
                    Title = h.Title,
                    Description = h.Description,
                    AttachmentUrls = h.AttachmentUrls,
                    Skill = h.Skill,
                    DueDate = h.DueDate,
                    TotalScore = h.TotalScore,
                    Status = h.Status,
                    CreatedAt = h.CreatedAt,
                    CreatedBy = h.CreatedBy,
                    TeacherName = h.Teacher != null ? h.Teacher.Name : null,
                    ClassName = h.Class != null ? h.Class.Name : null
                })
                .ToListAsync();

            return ApiResponse<IEnumerable<HomeworkDto>>.Ok(homeworks);
        }

        public async Task<ApiResponse<HomeworkDto>> CreateHomeworkAsync(HomeworkSaveDto dto)
        {
            var validation = await ValidateSaveDtoAsync(dto);
            if (validation.Error != null)
            {
                return ApiResponse<HomeworkDto>.Fail(validation.Error, validation.StatusCode);
            }

            var homework = new Homework
            {
                ClassId = dto.ClassId,
                TeacherId = dto.TeacherId,
                Title = dto.Title,
                Description = dto.Description,
                AttachmentUrls = dto.AttachmentUrls,
                Skill = dto.Skill,
                DueDate = dto.DueDate,
                TotalScore = dto.TotalScore,
                Status = dto.Status
            };

            await _homeworkRepository.AddAsync(homework);
            await _homeworkRepository.SaveChangesAsync();

            var result = new HomeworkDto
            {
                Id = homework.Id,
                ClassId = homework.ClassId,
                TeacherId = homework.TeacherId,
                Title = homework.Title,
                Description = homework.Description,
                AttachmentUrls = homework.AttachmentUrls,
                Skill = homework.Skill,
                DueDate = homework.DueDate,
                TotalScore = homework.TotalScore,
                Status = homework.Status,
                CreatedAt = homework.CreatedAt,
                CreatedBy = homework.CreatedBy
            };

            return ApiResponse<HomeworkDto>.Ok(result, "Thêm bài tập thành công");
        }

        public async Task<ApiResponse<HomeworkDto>> UpdateHomeworkAsync(int id, HomeworkSaveDto dto)
        {
            if (id <= 0)
            {
                return ApiResponse<HomeworkDto>.Fail("ERR_HOMEWORK_ID_REQUIRED", StatusCodes.Status400BadRequest);
            }

            var homework = await _homeworkRepository.GetByIdAsync(id);
            if (homework == null || homework.IsDeleted)
            {
                return ApiResponse<HomeworkDto>.Fail("ERR_HOMEWORK_NOT_FOUND", StatusCodes.Status404NotFound);
            }

            var validation = await ValidateSaveDtoAsync(dto);
            if (validation.Error != null)
            {
                return ApiResponse<HomeworkDto>.Fail(validation.Error, validation.StatusCode);
            }

            homework.ClassId = dto.ClassId;
            homework.TeacherId = dto.TeacherId;
            homework.Title = dto.Title;
            homework.Description = dto.Description;
            homework.AttachmentUrls = dto.AttachmentUrls;
            homework.Skill = dto.Skill;
            homework.DueDate = dto.DueDate;
            homework.TotalScore = dto.TotalScore;
            homework.Status = dto.Status;

            await _homeworkRepository.UpdateAsync(homework);
            await _homeworkRepository.SaveChangesAsync();

            var result = new HomeworkDto
            {
                Id = homework.Id,
                ClassId = homework.ClassId,
                TeacherId = homework.TeacherId,
                Title = homework.Title,
                Description = homework.Description,
                AttachmentUrls = homework.AttachmentUrls,
                Skill = homework.Skill,
                DueDate = homework.DueDate,
                TotalScore = homework.TotalScore,
                Status = homework.Status,
                CreatedAt = homework.CreatedAt,
                CreatedBy = homework.CreatedBy
            };

            return ApiResponse<HomeworkDto>.Ok(result, "Cập nhật bài tập thành công");
        }

        public async Task<ApiResponse<bool>> DeleteHomeworkAsync(int id)
        {
            if (id <= 0)
            {
                return ApiResponse<bool>.Fail("ERR_HOMEWORK_ID_REQUIRED", StatusCodes.Status400BadRequest);
            }

            var homework = await _homeworkRepository.GetByIdAsync(id);
            if (homework == null || homework.IsDeleted)
            {
                return ApiResponse<bool>.Fail("ERR_HOMEWORK_NOT_FOUND", StatusCodes.Status404NotFound);
            }

            await _homeworkRepository.DeleteAsync(homework);
            await _homeworkRepository.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "Xóa bài tập thành công");
        }

        public async Task<ApiResponse<IEnumerable<HomeworkSubmissionDto>>> GetSubmissionsByHomeworkAsync(int homeworkId)
        {
            if (homeworkId <= 0)
            {
                return ApiResponse<IEnumerable<HomeworkSubmissionDto>>.Fail("ERR_HOMEWORK_ID_REQUIRED", StatusCodes.Status400BadRequest);
            }

            var homeworkExists = await _homeworkRepository.ExistsAsync(h => h.Id == homeworkId && !h.IsDeleted);
            if (!homeworkExists)
            {
                return ApiResponse<IEnumerable<HomeworkSubmissionDto>>.Fail("ERR_HOMEWORK_NOT_FOUND", StatusCodes.Status404NotFound);
            }

            var submissions = await _homeworkSubmissionRepository.FindByCondition(s => s.HomeworkId == homeworkId && !s.IsDeleted)
                .Include(s => s.Student)
                .OrderByDescending(s => s.SubmitTime)
                .Select(s => new HomeworkSubmissionDto
                {
                    Id = s.Id,
                    HomeworkId = s.HomeworkId,
                    StudentId = s.StudentId,
                    Content = s.Content,
                    AttachmentUrls = s.AttachmentUrls,
                    SubmitTime = s.SubmitTime,
                    Score = s.Score,
                    TeacherFeedback = s.TeacherFeedback,
                    Status = s.Status,
                    StudentName = s.Student != null ? s.Student.Name : null,
                    StudentCode = s.Student != null ? s.Student.Code : null,
                    StudentEmail = s.Student != null ? s.Student.Email : null
                })
                .ToListAsync();

            return ApiResponse<IEnumerable<HomeworkSubmissionDto>>.Ok(submissions);
        }

        public async Task<ApiResponse<HomeworkSubmissionDto>> SubmitHomeworkAsync(HomeworkSubmissionSaveDto dto)
        {
            if (!dto.StudentId.HasValue || dto.StudentId.Value <= 0)
            {
                return ApiResponse<HomeworkSubmissionDto>.Fail("ERR_HOMEWORK_STUDENT_REQUIRED", StatusCodes.Status400BadRequest);
            }

            if (dto.HomeworkId <= 0)
            {
                return ApiResponse<HomeworkSubmissionDto>.Fail("ERR_HOMEWORK_ID_REQUIRED", StatusCodes.Status400BadRequest);
            }

            var homework = await _homeworkRepository.GetByIdAsync(dto.HomeworkId);
            if (homework == null || homework.IsDeleted)
            {
                return ApiResponse<HomeworkSubmissionDto>.Fail("ERR_HOMEWORK_NOT_FOUND", StatusCodes.Status404NotFound);
            }
            if (homework.Status == 0)
            {
                return ApiResponse<HomeworkSubmissionDto>.Fail("ERR_HOMEWORK_CLOSED", StatusCodes.Status400BadRequest);
            }

            var studentExists = await _dbContext.Students
                .AsNoTracking()
                .AnyAsync(s => s.Id == dto.StudentId.Value && !s.IsDeleted);
            if (!studentExists)
            {
                return ApiResponse<HomeworkSubmissionDto>.Fail("ERR_HOMEWORK_STUDENT_NOT_FOUND", StatusCodes.Status404NotFound);
            }

            var isEnrolled = await _dbContext.StudentClasses
                .AsNoTracking()
                .AnyAsync(sc => sc.StudentId == dto.StudentId.Value && sc.ClassId == homework.ClassId);
            if (!isEnrolled)
            {
                return ApiResponse<HomeworkSubmissionDto>.Fail("ERR_HOMEWORK_STUDENT_NOT_ENROLLED", StatusCodes.Status403Forbidden);
            }

            if (string.IsNullOrWhiteSpace(dto.Content) &&
                (dto.AttachmentUrls == null || !dto.AttachmentUrls.Any(url => !string.IsNullOrWhiteSpace(url))))
            {
                return ApiResponse<HomeworkSubmissionDto>.Fail("ERR_HOMEWORK_SUBMISSION_CONTENT_REQUIRED", StatusCodes.Status400BadRequest);
            }

            // Check existing
            var existing = await _homeworkSubmissionRepository
                .FindByCondition(s => s.HomeworkId == dto.HomeworkId && s.StudentId == dto.StudentId.Value && !s.IsDeleted, true)
                .FirstOrDefaultAsync();
            
            bool isLate = homework.DueDate.HasValue && DateTime.UtcNow > homework.DueDate.Value;

            if (existing != null)
            {
                existing.Content = dto.Content;
                existing.AttachmentUrls = dto.AttachmentUrls;
                existing.SubmitTime = DateTime.UtcNow;
                existing.Status = isLate ? 3 : 1; // 1: Submitted, 3: Late
                existing.Score = null;
                existing.TeacherFeedback = null;
                
                await _homeworkSubmissionRepository.UpdateAsync(existing);
            }
            else
            {
                existing = new HomeworkSubmission
                {
                    HomeworkId = dto.HomeworkId,
                    StudentId = dto.StudentId.Value,
                    Content = dto.Content,
                    AttachmentUrls = dto.AttachmentUrls,
                    SubmitTime = DateTime.UtcNow,
                    Status = isLate ? 3 : 1
                };
                await _homeworkSubmissionRepository.AddAsync(existing);
            }
            
            await _homeworkSubmissionRepository.SaveChangesAsync();

            var result = new HomeworkSubmissionDto
            {
                Id = existing.Id,
                HomeworkId = existing.HomeworkId,
                StudentId = existing.StudentId,
                Content = existing.Content,
                AttachmentUrls = existing.AttachmentUrls,
                SubmitTime = existing.SubmitTime,
                Score = existing.Score,
                TeacherFeedback = existing.TeacherFeedback,
                Status = existing.Status
            };

            return ApiResponse<HomeworkSubmissionDto>.Ok(result, "Nộp bài thành công");
        }

        public async Task<ApiResponse<HomeworkSubmissionDto>> GradeSubmissionAsync(int submissionId, HomeworkSubmissionGradeDto dto)
        {
            if (submissionId <= 0)
            {
                return ApiResponse<HomeworkSubmissionDto>.Fail("ERR_HOMEWORK_SUBMISSION_ID_REQUIRED", StatusCodes.Status400BadRequest);
            }

            var submission = await _homeworkSubmissionRepository.GetByIdAsync(submissionId);
            if (submission == null || submission.IsDeleted)
            {
                return ApiResponse<HomeworkSubmissionDto>.Fail("ERR_HOMEWORK_SUBMISSION_NOT_FOUND", StatusCodes.Status404NotFound);
            }

            var homework = await _homeworkRepository.GetByIdAsync(submission.HomeworkId);
            if (homework == null || homework.IsDeleted)
            {
                return ApiResponse<HomeworkSubmissionDto>.Fail("ERR_HOMEWORK_NOT_FOUND", StatusCodes.Status404NotFound);
            }
            if (dto.Score < 0)
            {
                return ApiResponse<HomeworkSubmissionDto>.Fail("ERR_HOMEWORK_SCORE_INVALID", StatusCodes.Status400BadRequest);
            }
            if (dto.Score > homework?.TotalScore)
            {
                return ApiResponse<HomeworkSubmissionDto>.Fail("ERR_HOMEWORK_SCORE_EXCEEDS_TOTAL", StatusCodes.Status400BadRequest);
            }

            submission.Score = dto.Score;
            submission.TeacherFeedback = dto.TeacherFeedback;
            submission.Status = 2; // 2: Graded

            await _homeworkSubmissionRepository.UpdateAsync(submission);
            await _homeworkSubmissionRepository.SaveChangesAsync();

            var result = new HomeworkSubmissionDto
            {
                Id = submission.Id,
                HomeworkId = submission.HomeworkId,
                StudentId = submission.StudentId,
                Content = submission.Content,
                AttachmentUrls = submission.AttachmentUrls,
                SubmitTime = submission.SubmitTime,
                Score = submission.Score,
                TeacherFeedback = submission.TeacherFeedback,
                Status = submission.Status
            };

            return ApiResponse<HomeworkSubmissionDto>.Ok(result, "Chấm điểm thành công");
        }

        private async Task<(string? Error, int StatusCode)> ValidateSaveDtoAsync(HomeworkSaveDto dto)
        {
            if (dto.ClassId <= 0)
                return ("ERR_HOMEWORK_CLASS_REQUIRED", StatusCodes.Status400BadRequest);
            if (dto.TeacherId <= 0)
                return ("ERR_HOMEWORK_TEACHER_REQUIRED", StatusCodes.Status400BadRequest);
            if (string.IsNullOrWhiteSpace(dto.Title))
                return ("ERR_HOMEWORK_TITLE_REQUIRED", StatusCodes.Status400BadRequest);
            if (dto.Title.Trim().Length > 500)
                return ("ERR_HOMEWORK_TITLE_MAX_LENGTH", StatusCodes.Status400BadRequest);
            if (dto.TotalScore < 0 || dto.TotalScore > 1000)
                return ("ERR_HOMEWORK_TOTAL_SCORE_INVALID", StatusCodes.Status400BadRequest);
            if (dto.Status is not 0 and not 1)
                return ("ERR_HOMEWORK_STATUS_INVALID", StatusCodes.Status400BadRequest);

            var classEntity = await _dbContext.Classes
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == dto.ClassId && !c.IsDeleted);
            if (classEntity == null)
                return ("ERR_HOMEWORK_CLASS_NOT_FOUND", StatusCodes.Status404NotFound);

            var teacherExists = await _dbContext.Teachers
                .AsNoTracking()
                .AnyAsync(t => t.Id == dto.TeacherId && !t.IsDeleted);
            if (!teacherExists)
                return ("ERR_HOMEWORK_TEACHER_NOT_FOUND", StatusCodes.Status404NotFound);

            if (classEntity.TeacherId.HasValue && classEntity.TeacherId.Value != dto.TeacherId)
                return ("ERR_HOMEWORK_TEACHER_NOT_ASSIGNED_TO_CLASS", StatusCodes.Status400BadRequest);

            dto.Title = dto.Title.Trim();
            dto.AttachmentUrls = dto.AttachmentUrls?
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Select(url => url.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return (null, StatusCodes.Status200OK);
        }
    }
}

