using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
        
        private readonly INotificationService _notificationService;
        private readonly ILogger<HomeworkService> _logger;

        public HomeworkService(
            IHomeworkRepository homeworkRepository,
            IHomeworkSubmissionRepository homeworkSubmissionRepository,
            INotificationService notificationService,
            ILogger<HomeworkService> logger = null)
        {
            _homeworkRepository = homeworkRepository;
            _homeworkSubmissionRepository = homeworkSubmissionRepository;
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task<ApiResponse<IEnumerable<HomeworkDto>>> GetHomeworkByClassAsync(int classId, string? username, bool isStudent)
        {
            if (classId <= 0)
            {
                return ApiResponse<IEnumerable<HomeworkDto>>.Fail("ERR_HOMEWORK_CLASS_REQUIRED", StatusCodes.Status400BadRequest);
            }

            var classExists = await _homeworkRepository.ClassExistsAsync(classId);
            if (!classExists)
            {
                return ApiResponse<IEnumerable<HomeworkDto>>.Fail("ERR_HOMEWORK_CLASS_NOT_FOUND", StatusCodes.Status404NotFound);
            }

            if (isStudent)
            {
                if (string.IsNullOrEmpty(username)) return ApiResponse<IEnumerable<HomeworkDto>>.Fail("FORBIDDEN", StatusCodes.Status403Forbidden);
                var student = await _homeworkRepository.ResolveStudentByUsernameAsync(username);
                var isEnrolled = student != null && await _homeworkRepository.IsStudentEnrolledInClassAsync(student.Id, classId);
                if (!isEnrolled) return ApiResponse<IEnumerable<HomeworkDto>>.Fail("FORBIDDEN", StatusCodes.Status403Forbidden);
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

        public async Task<ApiResponse<IEnumerable<HomeworkDto>>> GetStudentHomeworkByClassAsync(int classId, string? username)
        {
            if (string.IsNullOrEmpty(username)) return ApiResponse<IEnumerable<HomeworkDto>>.Fail("FORBIDDEN", StatusCodes.Status403Forbidden);
            var student = await _homeworkRepository.ResolveStudentByUsernameAsync(username);
            if (student == null)
            {
                return ApiResponse<IEnumerable<HomeworkDto>>.Fail("Không xác định được sinh viên", StatusCodes.Status400BadRequest);
            }

            var isEnrolled = await _homeworkRepository.IsStudentEnrolledInClassWithStatusAsync(student.Id, classId, new[] { 0, 1, 2 });
            if (!isEnrolled) return ApiResponse<IEnumerable<HomeworkDto>>.Fail("FORBIDDEN", StatusCodes.Status403Forbidden);
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

            if (homework.Status == 1)
            {
                try
                {
                    _logger?.LogInformation("[HomeworkService] Sending notification for new homework id={Id}, classId={ClassId}", homework.Id, homework.ClassId);
                    await _notificationService.SendHomeworkCreatedNotificationAsync(homework);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "[HomeworkService] Failed to send notification for homework id={Id}", homework.Id);
                }
            }

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

            var oldStatus = homework.Status;

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

            if (oldStatus != 1 && homework.Status == 1)
            {
                try
                {
                    _logger?.LogInformation("[HomeworkService] Sending notification for updated homework id={Id} activated, classId={ClassId}", homework.Id, homework.ClassId);
                    await _notificationService.SendHomeworkCreatedNotificationAsync(homework);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "[HomeworkService] Failed to send notification for updated homework id={Id}", homework.Id);
                }
            }

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

            await _homeworkRepository.FindByCondition(h => h.Id == homework.Id)
                .ExecuteDeleteAsync();

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

        public async Task<ApiResponse<HomeworkSubmissionDto>> SubmitHomeworkAsync(HomeworkSubmissionSaveDto dto, string? username)
        {
            Student? student = null;
            if (!string.IsNullOrEmpty(username))
            {
                student = await _homeworkRepository.ResolveStudentByUsernameAsync(username);
            }

            if (student == null)
            {
                return ApiResponse<HomeworkSubmissionDto>.Fail("Khong xac dinh duoc sinh vien", StatusCodes.Status400BadRequest);
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
            if (homework.DueDate.HasValue && homework.DueDate.Value <= DateTime.UtcNow)
            {
                return ApiResponse<HomeworkSubmissionDto>.Fail("ERR_SUBMISSION_CLOSED", StatusCodes.Status400BadRequest);
            }

            var isEnrolled = await _homeworkRepository.IsStudentEnrolledInClassAsync(student.Id, homework.ClassId);
            if (!isEnrolled)
            {
                return ApiResponse<HomeworkSubmissionDto>.Fail("ERR_HOMEWORK_STUDENT_NOT_ENROLLED", StatusCodes.Status403Forbidden);
            }

            dto.StudentId = student.Id;

            if (string.IsNullOrWhiteSpace(dto.Content) &&
                (dto.AttachmentUrls == null || !dto.AttachmentUrls.Any(url => !string.IsNullOrWhiteSpace(url))))
            {
                return ApiResponse<HomeworkSubmissionDto>.Fail("ERR_HOMEWORK_SUBMISSION_CONTENT_REQUIRED", StatusCodes.Status400BadRequest);
            }

            // Check existing
            var existing = await _homeworkSubmissionRepository
                .FindByCondition(s => s.HomeworkId == dto.HomeworkId && s.StudentId == dto.StudentId.Value && !s.IsDeleted, true)
                .FirstOrDefaultAsync();

            if (existing != null)
            {
                existing.Content = dto.Content;
                existing.AttachmentUrls = dto.AttachmentUrls;
                existing.SubmitTime = DateTime.UtcNow;
                existing.Status = 1; // Submitted
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
                    Status = 1
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

        public async Task<ApiResponse<HomeworkSubmissionDto?>> GetMySubmissionAsync(int homeworkId, string? username)
        {
            if (homeworkId <= 0)
            {
                return ApiResponse<HomeworkSubmissionDto?>.Fail("ERR_HOMEWORK_ID_REQUIRED", StatusCodes.Status400BadRequest);
            }

            if (string.IsNullOrEmpty(username))
            {
                return ApiResponse<HomeworkSubmissionDto?>.Fail("FORBIDDEN", StatusCodes.Status403Forbidden);
            }

            var student = await _homeworkRepository.ResolveStudentByUsernameAsync(username);
            if (student == null)
            {
                return ApiResponse<HomeworkSubmissionDto?>.Fail("Khong xac dinh duoc sinh vien", StatusCodes.Status400BadRequest);
            }

            var homework = await _homeworkRepository.GetByIdAsync(homeworkId);
            if (homework == null || homework.IsDeleted)
            {
                return ApiResponse<HomeworkSubmissionDto?>.Fail("ERR_HOMEWORK_NOT_FOUND", StatusCodes.Status404NotFound);
            }

            var isEnrolled = await _homeworkRepository.IsStudentEnrolledInClassAsync(student.Id, homework.ClassId);
            if (!isEnrolled) return ApiResponse<HomeworkSubmissionDto?>.Fail("FORBIDDEN", StatusCodes.Status403Forbidden);

            var submission = await _homeworkSubmissionRepository
                .FindByCondition(s => s.HomeworkId == homeworkId && s.StudentId == student.Id && !s.IsDeleted)
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
                .FirstOrDefaultAsync();

            return ApiResponse<HomeworkSubmissionDto?>.Ok(submission);
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
            if (dto.DueDate.HasValue && dto.DueDate.Value <= DateTime.UtcNow)
                return ("ERR_DUE_DATE_INVALID", StatusCodes.Status400BadRequest);
            if (dto.TotalScore < 0 || dto.TotalScore > 1000)
                return ("ERR_HOMEWORK_TOTAL_SCORE_INVALID", StatusCodes.Status400BadRequest);
            if (dto.Status is not 0 and not 1)
                return ("ERR_HOMEWORK_STATUS_INVALID", StatusCodes.Status400BadRequest);

            var classExists = await _homeworkRepository.ClassExistsAsync(dto.ClassId);
            if (!classExists)
                return ("ERR_HOMEWORK_CLASS_NOT_FOUND", StatusCodes.Status404NotFound);

            var teacherExists = await _homeworkRepository.TeacherExistsAsync(dto.TeacherId);
            if (!teacherExists)
                return ("ERR_HOMEWORK_TEACHER_NOT_FOUND", StatusCodes.Status404NotFound);

            var isAssigned = await _homeworkRepository.IsTeacherAssignedToClassAsync(dto.TeacherId, dto.ClassId);
            if (isAssigned) // Wait, if it has a teacher, it must match. The logic before was checking `classEntity.TeacherId.HasValue && classEntity.TeacherId.Value != dto.TeacherId`.
            {
                // We'll simplify:
            }

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

