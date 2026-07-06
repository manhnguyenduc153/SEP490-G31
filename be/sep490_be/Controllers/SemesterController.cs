using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using sep490_be.DTO.Semester;
using sep490_be.DTO.Teacher;
using sep490_be.DTO.Student;
using sep490_be.Services.Interfaces;
using sep490_be.Helpers.Authorization;
using sep490_be.DTO.Common;

namespace sep490_be.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SemesterController : ControllerBase
    {
        private readonly ISemesterService _service;

        public SemesterController(ISemesterService service)
        {
            _service = service;
        }

        // GET: api/Semester
        [HttpGet]
        [HasPermission(Permissions.Class.Class_View)]
        public async Task<IActionResult> GetAll()
        {
            var response = await _service.GetAllAsync();
            return StatusCode(response.StatusCode, response);
        }

        // GET: api/Semester/5
        [HttpGet("{id}")]
        [HasPermission(Permissions.Class.Class_View)]
        public async Task<IActionResult> GetById(int id)
        {
            var response = await _service.GetByIdAsync(id);
            return StatusCode(response.StatusCode, response);
        }

        // POST: api/Semester
        [HttpPost]
        [HasPermission(Permissions.Class.Class_Edit)]
        public async Task<IActionResult> Create([FromBody] SemesterSaveDto dto)
        {
            var response = await _service.CreateAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        // PUT: api/Semester/5
        [HttpPut("{id}")]
        [HasPermission(Permissions.Class.Class_Edit)]
        public async Task<IActionResult> Edit(int id, [FromBody] SemesterSaveDto dto)
        {
            dto.Id = id;
            var response = await _service.EditAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        // DELETE: api/Semester/5
        [HttpDelete("{id}")]
        [HasPermission(Permissions.Class.Class_Edit)]
        public async Task<IActionResult> Delete(int id)
        {
            var response = await _service.DeleteAsync(id);
            return StatusCode(response.StatusCode, response);
        }

        // GET: api/Semester/5/teacher/3/availability
        [HttpGet("{semesterId}/teacher/{teacherId}/availability")]
        [HasPermission(Permissions.Class.Class_View)]
        public async Task<IActionResult> GetTeacherAvailability(int semesterId, int teacherId)
        {
            var response = await _service.GetTeacherAvailabilitiesAsync(semesterId, teacherId);
            return StatusCode(response.StatusCode, response);
        }

        // POST: api/Semester/availability
        [HttpPost("availability")]
        [HasPermission(Permissions.Class.Class_Edit)]
        public async Task<IActionResult> SaveTeacherAvailability([FromBody] TeacherAvailabilitySaveDto dto)
        {
            var response = await _service.SaveTeacherAvailabilityAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        // GET: api/Semester/5/registrations
        [HttpGet("{semesterId}/registrations")]
        [HasPermission(Permissions.Class.Class_View)]
        public async Task<IActionResult> GetStudentRegistrations(
            int semesterId,
            [FromQuery] string? keyword,
            [FromQuery] int? courseId,
            [FromQuery] int? status,
            [FromQuery] int pageIndex = 1,
            [FromQuery] int pageSize = 10)
        {
            var response = await _service.GetStudentRegistrationsPagedAsync(semesterId, keyword, courseId, status, pageIndex, pageSize);
            return StatusCode(response.StatusCode, response);
        }

        // POST: api/Semester/registrations/import
        [HttpPost("registrations/import")]
        [HasPermission(Permissions.Class.Class_Edit)]
        public async Task<IActionResult> ImportStudentRegistrations([FromBody] List<StudentRegistrationSaveDto> dtos)
        {
            var response = await _service.ImportStudentRegistrationsAsync(dtos);
            return StatusCode(response.StatusCode, response);
        }
    }
}

