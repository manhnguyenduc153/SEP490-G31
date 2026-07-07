using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using sep490_be.DTO.Common;
using sep490_be.DTO.StudentGrade;
using sep490_be.Helpers.Authorization;
using sep490_be.Services.Interfaces;

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
        [HasPermission(Permissions.StudentGrade.StudentGrade_View)]
        public async Task<IActionResult> GetSettings(int classId)
        {
            var response = await _service.GetSettingsAsync(classId);
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("course/{courseId}/components")]
        [HasPermission(Permissions.StudentGrade.StudentGrade_View)]
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
        [HasPermission(Permissions.StudentGrade.StudentGrade_Edit)]
        public async Task<IActionResult> SaveOverrides(int classId, [FromBody] StudentGradeOverridesSaveDto dto)
        {
            var response = await _service.SaveOverridesAsync(classId, dto);
            return StatusCode(response.StatusCode, response);
        }
    }
}
