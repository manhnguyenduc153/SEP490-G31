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

        public HomeworkService(IHomeworkRepository homeworkRepository, IHomeworkSubmissionRepository homeworkSubmissionRepository)
        {
            _homeworkRepository = homeworkRepository;
            _homeworkSubmissionRepository = homeworkSubmissionRepository;
        }

        public async Task<ApiResponse<IEnumerable<HomeworkDto>>> GetHomeworkByClassAsync(int classId)
        {
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
            var homework = await _homeworkRepository.GetByIdAsync(id);
            if (homework == null || homework.IsDeleted)
            {
                return ApiResponse<HomeworkDto>.Fail("Không tìm thấy bài tập", StatusCodes.Status404NotFound);
            }

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
            var homework = await _homeworkRepository.GetByIdAsync(id);
            if (homework == null || homework.IsDeleted)
            {
                return ApiResponse<bool>.Fail("Không tìm thấy bài tập", StatusCodes.Status404NotFound);
            }

            await _homeworkRepository.DeleteAsync(homework);
            await _homeworkRepository.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "Xóa bài tập thành công");
        }

        public async Task<ApiResponse<IEnumerable<HomeworkSubmissionDto>>> GetSubmissionsByHomeworkAsync(int homeworkId)
        {
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
                return ApiResponse<HomeworkSubmissionDto>.Fail("Khong xac dinh duoc sinh vien", StatusCodes.Status400BadRequest);
            }

            var homework = await _homeworkRepository.GetByIdAsync(dto.HomeworkId);
            if (homework == null || homework.IsDeleted || homework.Status == 0)
            {
                return ApiResponse<HomeworkSubmissionDto>.Fail("Bài tập không khả dụng hoặc đã đóng", StatusCodes.Status400BadRequest);
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
            var submission = await _homeworkSubmissionRepository.GetByIdAsync(submissionId);
            if (submission == null || submission.IsDeleted)
            {
                return ApiResponse<HomeworkSubmissionDto>.Fail("Không tìm thấy bài nộp", StatusCodes.Status404NotFound);
            }

            var homework = await _homeworkRepository.GetByIdAsync(submission.HomeworkId);
            if (dto.Score > homework?.TotalScore)
            {
                return ApiResponse<HomeworkSubmissionDto>.Fail($"Điểm không được vượt quá tối đa ({homework.TotalScore})", StatusCodes.Status400BadRequest);
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
    }
}

