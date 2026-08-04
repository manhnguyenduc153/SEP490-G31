using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using sep490_be.DTO;
using sep490_be.DTO.Common;
using sep490_be.DTO.Homework;
using sep490_be.DTO.Attendance;
using sep490_be.DTO.Student;
using sep490_be.Helpers.Authorization;
using sep490_be.Models;
using sep490_be.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace sep490_be.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class StudentProgressController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;

        public StudentProgressController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        // GET api/StudentProgress/class/5
        [HttpGet("class/{classId}")]
        [HasPermission(Permissions.StudentProgress.StudentProgressPage)]
        public async Task<IActionResult> GetStudentProgress(int classId)
        {
            var student = await ResolveCurrentStudentAsync();
            if (student == null)
            {
                return BadRequest(ApiResponse<StudentProgressDto>.Fail("Không xác định được sinh viên", 400));
            }

            // Verify enrollment
            var isEnrolled = await _dbContext.StudentClasses
                .AsNoTracking()
                .AnyAsync(sc => sc.StudentId == student.Id &&
                                sc.ClassId == classId &&
                                sc.Class.Status == 1 &&
                                !sc.Class.IsDeleted);

            if (!isEnrolled)
            {
                return NotFound(ApiResponse<StudentProgressDto>.Fail("Không tìm thấy thông tin lớp học của sinh viên", 404));
            }

            var response = await GetProgressDataAsync(classId, student);
            return Ok(ApiResponse<StudentProgressDto>.Ok(response, "GET_STUDENT_PROGRESS_SUCCESS"));
        }

        // GET api/StudentProgress/class/5/student/10
        [HttpGet("class/{classId}/student/{studentId}")]
        [HasPermission(Permissions.ChildProgress.ChildProgressPage)]
        public async Task<IActionResult> GetChildProgress(int classId, int studentId)
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username))
            {
                return Forbid();
            }

            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserName == username || u.Id == username);
            var parentEmail = user?.Email ?? username;

            var parent = await _dbContext.ParentStudents
                .AsNoTracking()
                .Include(p => p.ParentStudentLinks)
                .FirstOrDefaultAsync(p => p.Email != null && p.Email.ToLower() == parentEmail.ToLower());

            if (parent == null || !parent.ParentStudentLinks.Any(link => link.StudentId == studentId && link.Status == 1))
            {
                return Forbid();
            }

            var student = await _dbContext.Students.FirstOrDefaultAsync(s => s.Id == studentId);
            if (student == null)
            {
                return NotFound(ApiResponse<StudentProgressDto>.Fail("Không tìm thấy sinh viên", 404));
            }

            var response = await GetProgressDataAsync(classId, student);
            return Ok(ApiResponse<StudentProgressDto>.Ok(response, "GET_CHILD_PROGRESS_SUCCESS"));
        }

        private async Task<StudentProgressDto> GetProgressDataAsync(int classId, Student student)
        {
            // 1. Fetch published homeworks (status = 1) for the class
            var homeworks = await _dbContext.Homeworks
                .AsNoTracking()
                .Where(h => h.ClassId == classId && !h.IsDeleted && h.Status == 1)
                .Include(h => h.Teacher)
                .Include(h => h.Class)
                .OrderByDescending(h => h.CreatedAt)
                .ToListAsync();

            // 2. Fetch homework submissions for the student in this class
            var submissions = await _dbContext.HomeworkSubmissions
                .AsNoTracking()
                .Where(s => s.StudentId == student.Id && !s.IsDeleted)
                .ToListAsync();

            var submissionMap = submissions
                .GroupBy(s => s.HomeworkId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.SubmitTime).First());

            var homeworkDtos = homeworks.Select(h => {
                submissionMap.TryGetValue(h.Id, out var sub);
                return new HomeworkWithSubmissionDto
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
                    TeacherName = h.Teacher?.Name,
                    ClassName = h.Class?.Name,
                    Submission = sub != null ? new HomeworkSubmissionDto
                    {
                        Id = sub.Id,
                        HomeworkId = sub.HomeworkId,
                        StudentId = sub.StudentId,
                        Content = sub.Content,
                        AttachmentUrls = sub.AttachmentUrls,
                        SubmitTime = sub.SubmitTime,
                        Score = sub.Score,
                        TeacherFeedback = sub.TeacherFeedback,
                        Status = sub.Status,
                        StudentName = student.Name,
                        StudentCode = student.Code,
                        StudentEmail = student.Email
                    } : null
                };
            }).ToList();

            // 3. Fetch attendance details for the student in this class
            var schedules = await _dbContext.ClassSchedules
                .AsNoTracking()
                .Where(schedule => schedule.ClassId == classId && !schedule.IsDeleted)
                .OrderBy(schedule => schedule.ScheduleDate)
                .ThenBy(schedule => schedule.LessonNo)
                .Select(schedule => new
                {
                    Schedule = schedule,
                    Attendance = schedule.Attendances
                        .Where(a => a.StudentId == student.Id && !a.IsDeleted)
                        .OrderByDescending(a => a.UpdatedAt)
                        .FirstOrDefault()
                })
                .ToListAsync();

            var attendanceDtos = schedules.Select(x => new MyAttendanceSessionDto
            {
                ScheduleId = x.Schedule.Id,
                LessonNo = x.Schedule.LessonNo ?? 0,
                Date = x.Schedule.ScheduleDate,
                Status = x.Attendance?.Status ?? -1,
                StatusName = x.Attendance == null
                    ? "NOT_MARKED"
                    : ((AttendanceStatus)x.Attendance.Status).GetStringValue(),
                Description = x.Attendance?.Description
            }).ToList();

            return new StudentProgressDto
            {
                Homeworks = homeworkDtos,
                AttendanceSessions = attendanceDtos
            };
        }

        private async Task<Student?> ResolveCurrentStudentAsync()
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return null;

            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserName == username || u.Id == username);
            var email = user?.Email ?? username;

            return await _dbContext.Students.FirstOrDefaultAsync(s =>
                (s.Email != null && s.Email.ToLower() == email.ToLower()) ||
                (s.Code != null && s.Code.ToLower() == username.ToLower()) ||
                (s.Email != null && s.Email.ToLower() == username.ToLower()));
        }
    }
}
