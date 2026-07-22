using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using sep490_be.DTO.Class;
using sep490_be.DTO.Common;
using sep490_be.Services.Interfaces;
using sep490_be.Helpers.Authorization;

namespace sep490_be.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ClassController : ControllerBase
    {
        private readonly IClassService _service;
        private readonly IScheduleOptimizationService _optService;

        public ClassController(IClassService service, IScheduleOptimizationService optService)
        {
            _service = service;
            _optService = optService;
        }

        // GET: api/Class
        [HttpGet]
        [HasPermission(Permissions.Class.Class_View)]
        public async Task<IActionResult> GetAll([FromQuery] ClassSearchDto searchDto)
        {
            var response = await _service.GetAllAsync(searchDto);
            return StatusCode(response.StatusCode, response);
        }

        // GET: api/Class/5
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var username = User.Identity?.Name;
            var response = await _service.GetByIdAsync(id, username);
            return StatusCode(response.StatusCode, response);
        }

        // GET: api/Class/teaching-classes
        [HttpGet("teaching-classes")]
        [HasPermission(Permissions.Class.Class_TeacherView)]
        public async Task<IActionResult> GetTeacherClasses([FromQuery] ClassSearchDto searchDto)
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username))
            {
                return Unauthorized();
            }
            var response = await _service.GetTeacherClassesAsync(username, searchDto);
            return StatusCode(response.StatusCode, response);
        }

        // GET: api/Class/my-classes
        [HttpGet("my-classes")]
        [HasPermission(Permissions.Class.Class_StudentView)]
        public async Task<IActionResult> GetStudentClasses([FromQuery] ClassSearchDto searchDto)
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username))
            {
                return Unauthorized();
            }
            var response = await _service.GetStudentClassesAsync(username, searchDto);
            return StatusCode(response.StatusCode, response);
        }

        // POST: api/Class
        [HttpPost]
        [HasPermission(Permissions.Class.Class_Create)]
        public async Task<IActionResult> Create([FromBody] ClassSaveDto dto)
        {
            var response = await _service.CreateAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        // PUT: api/Class/5
        [HttpPut("{id}")]
        [HasPermission(Permissions.Class.Class_Edit)]
        public async Task<IActionResult> Edit(int id, [FromBody] ClassSaveDto dto)
        {
            dto.Id = id;
            var response = await _service.EditAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        // DELETE: api/Class/5
        [HttpDelete("{id}")]
        [HasPermission(Permissions.Class.Class_Delete)]
        public async Task<IActionResult> Delete(int id)
        {
            var response = await _service.DeleteAsync(id);
            return StatusCode(response.StatusCode, response);
        }

        // POST: api/Class/5/deactive
        [HttpPost("{id}/deactive")]
        [HasPermission(Permissions.Class.Class_Delete)]
        public async Task<IActionResult> Deactive(int id)
        {
            var response = await _service.DeactiveAsync(id);
            return StatusCode(response.StatusCode, response);
        }

        // GET: api/Class/TeacherSchedules
        [HttpGet("TeacherSchedules")]
        [HasPermission(Permissions.ClassSchedule.ClassSchedule_TeacherView)]
        public async Task<IActionResult> GetTeacherSchedules()
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username))
            {
                return Unauthorized();
            }
            var response = await _service.GetTeacherSchedulesAsync(username);
            return StatusCode(response.StatusCode, response);
        }

        // GET: api/Class/StudentSchedules
        [HttpGet("StudentSchedules")]
        [HasPermission(Permissions.ClassSchedule.ClassSchedule_StudentView)]
        public async Task<IActionResult> GetStudentSchedules()
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username))
            {
                return Unauthorized();
            }
            var response = await _service.GetStudentSchedulesAsync(username);
            return StatusCode(response.StatusCode, response);
        }

        // GET: api/Class/ChildSchedules?studentId=5
        [HttpGet("ChildSchedules")]
        [HasPermission(Permissions.ParentStudent.ParentStudent_View)]
        public async Task<IActionResult> GetChildSchedules([FromQuery] int studentId)
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username))
            {
                return Unauthorized();
            }
            var response = await _service.GetChildSchedulesAsync(username, studentId);
            return StatusCode(response.StatusCode, response);
        }

        // POST: api/Class/check-conflict
        [HttpPost("check-conflict")]
        [HasPermission(Permissions.Class.Class_View)]
        public async Task<IActionResult> CheckConflict([FromBody] ClassSaveDto dto)
        {
            var response = await _optService.CheckConflictAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        // POST: api/Class/auto-schedule
        [HttpPost("auto-schedule")]
        [HasPermission(Permissions.Class.Class_Edit)]
        public async Task<IActionResult> AutoSchedule([FromBody] AutoScheduleRequestDto request)
        {
            var response = await _optService.AutoScheduleAsync(request.ClassIds, request.Constraints);
            return StatusCode(response.StatusCode, response);
        }

        // GET: api/Class/Schedules
        [HttpGet("Schedules")]
        [HasPermission(Permissions.ClassSchedule.ClassSchedule_View)]
        public async Task<IActionResult> GetClassSchedules()
        {
            var response = await _service.GetClassSchedulesAsync();
            return StatusCode(response.StatusCode, response);
        }

        // GET: api/Class/fixed-slots — returns the 5 canonical time slots hard-coded in the system
        [HttpGet("fixed-slots")]
        [HasPermission(Permissions.Class.Class_View)]
        public IActionResult GetFixedSlots()
        {
            var slots = sep490_be.Enums.FixedTimeSlot.All.Select(fs => new
            {
                index     = fs.Index,
                name      = fs.Name,
                startTime = fs.StartStr,
                endTime   = fs.EndStr
            });
            return Ok(new { success = true, data = slots });
        }

        // POST: api/Class/auto-schedule-semester
        [HttpPost("auto-schedule-semester")]
        [HasPermission(Permissions.Class.Class_Edit)]
        public async Task<IActionResult> AutoScheduleSemester([FromBody] AutoScheduleSemesterRequestDto request)
        {
            var response = await _optService.AutoScheduleSemesterAsync(request);
            return StatusCode(response.StatusCode, response);
        }
    }
}

