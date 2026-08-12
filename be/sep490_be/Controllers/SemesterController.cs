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
        [HasPermission(Permissions.Semester.Semester_View)]
        public async Task<IActionResult> GetAll()
        {
            var response = await _service.GetAllAsync();
            return StatusCode(response.StatusCode, response);
        }

        // GET: api/Semester/5
        [HttpGet("{id}")]
        [HasPermission(Permissions.Semester.Semester_View)]
        public async Task<IActionResult> GetById(int id)
        {
            var response = await _service.GetByIdAsync(id);
            return StatusCode(response.StatusCode, response);
        }

        // POST: api/Semester
        [HttpPost]
        [HasPermission(Permissions.Semester.Semester_Create)]
        public async Task<IActionResult> Create([FromBody] SemesterSaveDto dto)
        {
            var response = await _service.CreateAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        // PUT: api/Semester/5
        [HttpPut("{id}")]
        [HasPermission(Permissions.Semester.Semester_Edit)]
        public async Task<IActionResult> Edit(int id, [FromBody] SemesterSaveDto dto)
        {
            dto.Id = id;
            var response = await _service.EditAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        // DELETE: api/Semester/5
        [HttpDelete("{id}")]
        [HasPermission(Permissions.Semester.Semester_Delete)]
        public async Task<IActionResult> Delete(int id)
        {
            var response = await _service.DeleteAsync(id);
            return StatusCode(response.StatusCode, response);
        }

        // GET: api/Semester/5/teacher/3/availability
        [HttpGet("{semesterId}/teacher/{teacherId}/availability")]
        [HasPermission(Permissions.Semester.Semester_View)]
        public async Task<IActionResult> GetTeacherAvailability(int semesterId, int teacherId)
        {
            var response = await _service.GetTeacherAvailabilitiesAsync(semesterId, teacherId);
            return StatusCode(response.StatusCode, response);
        }

        // GET: api/Semester/5/teachers/availabilities
        [HttpGet("{semesterId}/teachers/availabilities")]
        [HasPermission(Permissions.Semester.Semester_View)]
        public async Task<IActionResult> GetSemesterTeacherAvailabilities(int semesterId)
        {
            var response = await _service.GetAllTeacherAvailabilitiesAsync(semesterId);
            return StatusCode(response.StatusCode, response);
        }

        // GET: api/Semester/5/teacher/3/has-schedules
        [HttpGet("{semesterId}/teacher/{teacherId}/has-schedules")]
        [HasPermission(Permissions.Semester.Semester_View)]
        public async Task<IActionResult> CheckTeacherHasSchedules(int semesterId, int teacherId)
        {
            var response = await _service.CheckTeacherHasSchedulesAsync(semesterId, teacherId);
            return StatusCode(response.StatusCode, response);
        }

        // POST: api/Semester/availability
        [HttpPost("availability")]
        [HasPermission(Permissions.Semester.Semester_Edit)]
        public async Task<IActionResult> SaveTeacherAvailability([FromBody] TeacherAvailabilitySaveDto dto)
        {
            var response = await _service.SaveTeacherAvailabilityAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        // GET: api/Semester/5/registrations
        [HttpGet("{semesterId}/registrations")]
        [HasPermission(Permissions.StudentRegistration.StudentRegistration_View)]
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
        [HasPermission(Permissions.StudentRegistration.StudentRegistration_Import)]
        public async Task<IActionResult> ImportStudentRegistrations([FromBody] List<StudentRegistrationSaveDto> dtos)
        {
            var response = await _service.ImportStudentRegistrationsAsync(dtos);
            return StatusCode(response.StatusCode, response);
        }

        // POST: api/Semester/registrations
        [HttpPost("registrations")]
        [HasPermission(Permissions.StudentRegistration.StudentRegistration_Create)]
        public async Task<IActionResult> CreateStudentRegistration([FromBody] StudentRegistrationSaveDto dto)
        {
            var response = await _service.CreateStudentRegistrationAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        // PUT: api/Semester/registrations/5
        [HttpPut("registrations/{id}")]
        [HasPermission(Permissions.StudentRegistration.StudentRegistration_Edit)]
        public async Task<IActionResult> EditStudentRegistration(int id, [FromBody] StudentRegistrationSaveDto dto)
        {
            var response = await _service.EditStudentRegistrationAsync(id, dto);
            return StatusCode(response.StatusCode, response);
        }

        // DELETE: api/Semester/registrations/5
        [HttpDelete("registrations/{id}")]
        [HasPermission(Permissions.StudentRegistration.StudentRegistration_Delete)]
        public async Task<IActionResult> DeleteStudentRegistration(int id)
        {
            var response = await _service.DeleteStudentRegistrationAsync(id);
            return StatusCode(response.StatusCode, response);
        }
    }
}

