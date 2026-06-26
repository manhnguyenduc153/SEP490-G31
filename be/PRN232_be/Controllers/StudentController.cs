using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PRN232_be.DTO.Student;
using PRN232_be.DTO.Common;
using PRN232_be.Services.Interfaces;
using PRN232_be.Helpers.Authorization;

namespace PRN232_be.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class StudentController : ControllerBase
    {
        private readonly IStudentService _service;

        public StudentController(IStudentService service)
        {
            _service = service;
        }

        // GET: api/Student
        [HttpGet]
        [HasPermission(Permissions.Student.Student_View)]
        public async Task<IActionResult> GetAll([FromQuery] StudentSearchDto searchDto)
        {
            var response = await _service.GetAllAsync(searchDto);
            return StatusCode(response.StatusCode, response);
        }

        // GET: api/Student/5
        [HttpGet("{id}")]
        [HasPermission(Permissions.Student.Student_View)]
        public async Task<IActionResult> GetById(int id)
        {
            var response = await _service.GetByIdAsync(id);
            return StatusCode(response.StatusCode, response);
        }

        // POST: api/Student
        [HttpPost]
        [HasPermission(Permissions.Student.Student_Create)]
        public async Task<IActionResult> Create([FromBody] StudentSaveDto dto)
        {
            var response = await _service.CreateAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        // PUT: api/Student/5
        [HttpPut("{id}")]
        [HasPermission(Permissions.Student.Student_Edit)]
        public async Task<IActionResult> Edit(int id, [FromBody] StudentSaveDto dto)
        {
            dto.Id = id;
            var response = await _service.EditAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        // DELETE: api/Student/5
        [HttpDelete("{id}")]
        [HasPermission(Permissions.Student.Student_Delete)]
        public async Task<IActionResult> Delete(int id)
        {
            var response = await _service.DeleteAsync(id);
            return StatusCode(response.StatusCode, response);
        }

        // POST: api/Student/5/deactive
        [HttpPost("{id}/deactive")]
        [HasPermission(Permissions.Student.Student_Delete)]
        public async Task<IActionResult> Deactive(int id)
        {
            var response = await _service.DeactiveAsync(id);
            return StatusCode(response.StatusCode, response);
        }
    }
}
