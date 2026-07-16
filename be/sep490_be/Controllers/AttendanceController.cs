using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using sep490_be.DTO.Attendance;
using sep490_be.DTO.Common;
using sep490_be.Services.Interfaces;
using sep490_be.Helpers.Authorization;
using System.Threading.Tasks;
using System.Security.Claims;

namespace sep490_be.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AttendanceController : ControllerBase
    {
        private readonly IAttendanceService _service;

        public AttendanceController(IAttendanceService service)
        {
            _service = service;
        }

        // GET: api/Attendance/schedule/5
        [HttpGet("schedule/{scheduleId}")]
        [HasPermission(Permissions.Attendance.Attendance_View)]
        public async Task<IActionResult> GetByScheduleId(int scheduleId)
        {
            var response = await _service.GetByScheduleIdAsync(scheduleId);
            return StatusCode(response.StatusCode, response);
        }

        // POST: api/Attendance/bulk-save
        [HttpPost("bulk-save")]
        [HasPermission(Permissions.Attendance.Attendance_SaveAttendance)]
        public async Task<IActionResult> BulkSave([FromBody] AttendanceBulkSaveDto dto)
        {
            var response = await _service.BulkSaveAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        // GET: api/Attendance/class/{classId}/report
        [HttpGet("class/{classId}/report")]
        [HasPermission(Permissions.Attendance.Attendance_View)]
        public async Task<IActionResult> GetReportByClassId(int classId)
        {
            var response = await _service.GetReportByClassIdAsync(classId);
            return StatusCode(response.StatusCode, response);
        }

        // GET: api/Attendance/my
        [HttpGet("my")]
        public async Task<IActionResult> GetMyAttendance()
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

            var response = await _service.GetMyAttendanceAsync(identifiers!);
            return StatusCode(response.StatusCode, response);
        }

        // GET: api/Attendance/my/class/5
        [HttpGet("my/class/{classId}")]
        public async Task<IActionResult> GetMyAttendanceDetails(int classId)
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

            var response = await _service.GetMyAttendanceDetailsAsync(classId, identifiers!);
            return StatusCode(response.StatusCode, response);
        }
    }
}

