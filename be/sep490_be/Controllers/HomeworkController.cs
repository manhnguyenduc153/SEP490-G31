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
        public HomeworkController(IHomeworkService homeworkService)
        {
            _homeworkService = homeworkService;
        }

        [HttpGet("class/{classId}")]
        public async Task<IActionResult> GetHomeworkByClass(int classId)
        {
            if (!HasAnyPermission(Permissions.StudentHomework.StudentHomework_View, Permissions.HomeworkManagement.HomeworkManagement_View))
                return Forbid();
            if (classId <= 0)
                return BadRequest(ApiResponse<IEnumerable<HomeworkDto>>.Fail("ERR_HOMEWORK_CLASS_REQUIRED", 400));

            var username = User.Identity?.Name;
            var isStudent = User.IsInRole("Student");
            var result = await _homeworkService.GetHomeworkByClassAsync(classId, username, isStudent);
            if (result.StatusCode == 403) return Forbid();
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("class/{classId}/student")]
        [HasPermission(Permissions.StudentHomework.StudentHomework_View)]
        public async Task<IActionResult> GetStudentHomeworkByClass(int classId)
        {
            var username = User.Identity?.Name;
            var result = await _homeworkService.GetStudentHomeworkByClassAsync(classId, username);
            if (result.StatusCode == 403) return Forbid();
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
            return StatusCode(result.StatusCode, result);
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
            return StatusCode(result.StatusCode, result);
        }

        [HttpDelete("{id}")]
        [HasPermission(Permissions.HomeworkManagement.HomeworkManagement_Delete)]
        public async Task<IActionResult> DeleteHomework(int id)
        {
            var result = await _homeworkService.DeleteHomeworkAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("{id}/submissions")]
        [HasPermission(Permissions.HomeworkManagement.HomeworkManagement_View)]
        public async Task<IActionResult> GetSubmissions(int id)
        {
            var result = await _homeworkService.GetSubmissionsByHomeworkAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("{id}/my-submission")]
        [HasPermission(Permissions.StudentHomework.StudentHomework_View)]
        public async Task<IActionResult> GetMySubmission(int id)
        {
            if (id <= 0)
            {
                return BadRequest(ApiResponse<HomeworkSubmissionDto?>.Fail("ERR_HOMEWORK_ID_REQUIRED", 400));
            }

            var username = User.Identity?.Name;
            var result = await _homeworkService.GetMySubmissionAsync(id, username);
            if (result.StatusCode == 403) return Forbid();
            
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("submissions/{submissionId}/grade")]
        [HasPermission(Permissions.HomeworkManagement.HomeworkManagement_Edit)]
        public async Task<IActionResult> GradeSubmission(int submissionId, [FromBody] HomeworkSubmissionGradeDto dto)
        {
            var result = await _homeworkService.GradeSubmissionAsync(submissionId, dto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("submit")]
        [HasPermission(Permissions.StudentHomework.StudentHomework_Submit)]
        public async Task<IActionResult> SubmitHomework([FromBody] HomeworkSubmissionSaveDto dto)
        {
            var username = User.Identity?.Name;
            var result = await _homeworkService.SubmitHomeworkAsync(dto, username);
            if (result.StatusCode == 403) return Forbid();
            return StatusCode(result.StatusCode, result);
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

