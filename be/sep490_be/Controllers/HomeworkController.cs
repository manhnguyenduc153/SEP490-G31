using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sep490_be.DTO;
using sep490_be.DTO.Common;
using sep490_be.DTO.Homework;
using sep490_be.Helpers;
using sep490_be.Helpers.Authorization;
using sep490_be.Models;
using sep490_be.Services.Interfaces;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace sep490_be.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class HomeworkController : ControllerBase
    {
        private readonly IHomeworkService _homeworkService;
        private readonly ApplicationDbContext _dbContext;

        public HomeworkController(IHomeworkService homeworkService, ApplicationDbContext dbContext)
        {
            _homeworkService = homeworkService;
            _dbContext = dbContext;
        }

        [HttpGet("class/{classId}")]
        public async Task<IActionResult> GetHomeworkByClass(int classId)
        {
            if (!HasAnyPermission(Permissions.StudentHomework.StudentHomework_View, Permissions.HomeworkManagement.HomeworkManagement_View))
                return Forbid();
            if (User.IsInRole("Student"))
            {
                var student = await ResolveCurrentStudentAsync();
                var isEnrolled = student != null && await _dbContext.StudentClasses
                    .AnyAsync(sc => sc.StudentId == student.Id && sc.ClassId == classId);
                if (!isEnrolled) return Forbid();
            }
            var result = await _homeworkService.GetHomeworkByClassAsync(classId);
            return Ok(result);
        }

        [HttpPost]
        [HasPermission(Permissions.HomeworkManagement.HomeworkManagement_Create)]
        public async Task<IActionResult> CreateHomework([FromBody] HomeworkSaveDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _homeworkService.CreateHomeworkAsync(dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPut("{id}")]
        [HasPermission(Permissions.HomeworkManagement.HomeworkManagement_Edit)]
        public async Task<IActionResult> UpdateHomework(int id, [FromBody] HomeworkSaveDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _homeworkService.UpdateHomeworkAsync(id, dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpDelete("{id}")]
        [HasPermission(Permissions.HomeworkManagement.HomeworkManagement_Delete)]
        public async Task<IActionResult> DeleteHomework(int id)
        {
            var result = await _homeworkService.DeleteHomeworkAsync(id);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet("{id}/submissions")]
        [HasPermission(Permissions.HomeworkManagement.HomeworkManagement_View)]
        public async Task<IActionResult> GetSubmissions(int id)
        {
            var result = await _homeworkService.GetSubmissionsByHomeworkAsync(id);
            return Ok(result);
        }

        [HttpGet("{id}/my-submission")]
        [HasPermission(Permissions.StudentHomework.StudentHomework_View)]
        public async Task<IActionResult> GetMySubmission(int id)
        {
            var student = await ResolveCurrentStudentAsync();
            if (student == null)
            {
                return BadRequest(ApiResponse<HomeworkSubmissionDto>.Fail("Khong xac dinh duoc sinh vien", 400));
            }

            var submission = await _dbContext.HomeworkSubmissions
                .AsNoTracking()
                .Include(s => s.Student)
                .Where(s => s.HomeworkId == id && s.StudentId == student.Id && !s.IsDeleted)
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

            return Ok(ApiResponse<HomeworkSubmissionDto?>.Ok(submission));
        }

        [HttpPost("submissions/{submissionId}/grade")]
        [HasPermission(Permissions.HomeworkManagement.HomeworkManagement_Edit)]
        public async Task<IActionResult> GradeSubmission(int submissionId, [FromBody] HomeworkSubmissionGradeDto dto)
        {
            var result = await _homeworkService.GradeSubmissionAsync(submissionId, dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPost("submit")]
        [HasPermission(Permissions.StudentHomework.StudentHomework_Submit)]
        public async Task<IActionResult> SubmitHomework([FromBody] HomeworkSubmissionSaveDto dto)
        {
            var student = await ResolveCurrentStudentAsync();

            if (student == null && dto.StudentId.HasValue && dto.StudentId.Value > 0)
            {
                student = await _dbContext.Students.FirstOrDefaultAsync(s => s.Id == dto.StudentId.Value);
            }

            if (student == null)
            {
                return BadRequest(ApiResponse<HomeworkSubmissionDto>.Fail("Khong xac dinh duoc sinh vien", 400));
            }

            var isEnrolled = await _dbContext.Homeworks
                .Where(homework => homework.Id == dto.HomeworkId && !homework.IsDeleted)
                .AnyAsync(homework => _dbContext.StudentClasses
                    .Any(sc => sc.StudentId == student.Id && sc.ClassId == homework.ClassId));
            if (!isEnrolled) return Forbid();

            dto.StudentId = student.Id;
            var result = await _homeworkService.SubmitHomeworkAsync(dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        private async Task<Student?> ResolveCurrentStudentAsync()
        {
            var identifiers = User.Claims
                .Where(c =>
                    c.Type == ClaimTypes.Email ||
                    c.Type == ClaimTypes.Name ||
                    c.Type == ClaimTypes.NameIdentifier ||
                    c.Type == "email" ||
                    c.Type == "sub" ||
                    c.Type == "unique_name" ||
                    c.Type == "preferred_username")
                .Select(c => c.Value)
                .Append(User.Identity?.Name)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct()
                .ToList();

            if (identifiers.Count == 0)
            {
                return null;
            }

            return await _dbContext.Students.FirstOrDefaultAsync(s =>
                (s.Email != null && identifiers.Contains(s.Email)) ||
                (s.Code != null && identifiers.Contains(s.Code)));
        }

        private bool HasAnyPermission(params string[] permissions)
        {
            if (User.IsInRole("Admin")) return true;
            return User.Claims.Any(c =>
                c.Type.Equals("Permission", StringComparison.OrdinalIgnoreCase) &&
                permissions.Contains(c.Value, StringComparer.OrdinalIgnoreCase));
        }
    }
}

