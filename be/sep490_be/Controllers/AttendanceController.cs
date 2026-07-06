using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using sep490_be.DTO.Attendance;
using sep490_be.DTO.Common;
using sep490_be.Services.Interfaces;
using sep490_be.Helpers.Authorization;
using System.Threading.Tasks;

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
        [HasPermission(Permissions.Attendance.Attendance_Create)]
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
    }
}

