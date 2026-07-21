using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using sep490_be.DTO.Common;
using sep490_be.DTO.StudentGrade;
using sep490_be.Helpers.Authorization;
using sep490_be.Services.Interfaces;
using System.Security.Claims;

namespace sep490_be.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class StudentGradeController : ControllerBase
    {
        private readonly IStudentGradeService _service;

        public StudentGradeController(IStudentGradeService service)
        {
            _service = service;
        }

        [HttpGet("class/{classId}/settings")]
        [HasPermission(Permissions.StudentGrade.StudentGrade_ViewSettings)]
        public async Task<IActionResult> GetSettings(int classId)
        {
            var response = await _service.GetSettingsAsync(classId);
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("my")]
        [HasPermission(Permissions.StudentGrade.StudentGrade_ViewOwnGrades)]
        public async Task<IActionResult> GetMyGrades()
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

            var response = await _service.GetMyGradesAsync(identifiers!);
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("child-grades")]
        public async Task<IActionResult> GetChildGrades([FromQuery] int studentId)
        {
            var username = User.Identity?.Name ?? "";
            var response = await _service.GetChildGradesAsync(username, studentId);
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("course/{courseId}/components")]
        [HasPermission(Permissions.StudentGrade.StudentGrade_ViewSettings)]
        public async Task<IActionResult> GetCourseComponents(int courseId)
        {
            var response = await _service.GetCourseComponentsAsync(courseId);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPut("course/{courseId}/components")]
        [HasPermission(Permissions.StudentGrade.StudentGrade_Edit)]
        public async Task<IActionResult> SaveCourseComponents(int courseId, [FromBody] ClassGradeComponentsSaveDto dto)
        {
            var response = await _service.SaveCourseComponentsAsync(courseId, dto);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPut("class/{classId}/overrides")]
        [HasPermission(Permissions.StudentGrade.StudentGrade_SaveGrade)]
        public async Task<IActionResult> SaveOverrides(int classId, [FromBody] StudentGradeOverridesSaveDto dto)
        {
            var response = await _service.SaveOverridesAsync(classId, dto);
            return StatusCode(response.StatusCode, response);
        }
    }
}
