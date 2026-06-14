using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PRN232_be.DTO.Teacher;
using PRN232_be.DTO.Common;
using PRN232_be.Services.Interfaces;
using PRN232_be.Helpers.Authorization;

namespace PRN232_be.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TeacherController : ControllerBase
    {
        private readonly ITeacherService _service;

        public TeacherController(ITeacherService service)
        {
            _service = service;
        }

        // GET: api/Teacher
        [HttpGet]
        [HasPermission(Permissions.Teacher.Teacher_View)]
        public async Task<IActionResult> GetAll([FromQuery] TeacherSearchDto searchDto)
        {
            var response = await _service.GetAllAsync(searchDto);
            return StatusCode(response.StatusCode, response);
        }

        // GET: api/Teacher/5
        [HttpGet("{id}")]
        [HasPermission(Permissions.Teacher.Teacher_View)]
        public async Task<IActionResult> GetById(int id)
        {
            var response = await _service.GetByIdAsync(id);
            return StatusCode(response.StatusCode, response);
        }

        // POST: api/Teacher
        [HttpPost]
        [HasPermission(Permissions.Teacher.Teacher_Create)]
        public async Task<IActionResult> Create([FromBody] TeacherSaveDto dto)
        {
            var response = await _service.CreateAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        // PUT: api/Teacher/5
        [HttpPut("{id}")]
        [HasPermission(Permissions.Teacher.Teacher_Edit)]
        public async Task<IActionResult> Edit(int id, [FromBody] TeacherSaveDto dto)
        {
            dto.Id = id;
            var response = await _service.EditAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        // DELETE: api/Teacher/5
        [HttpDelete("{id}")]
        [HasPermission(Permissions.Teacher.Teacher_Delete)]
        public async Task<IActionResult> Delete(int id)
        {
            var response = await _service.DeleteAsync(id);
            return StatusCode(response.StatusCode, response);
        }

        // POST: api/Teacher/5/deactive
        [HttpPost("{id}/deactive")]
        [HasPermission(Permissions.Teacher.Teacher_Delete)]
        public async Task<IActionResult> Deactive(int id)
        {
            var response = await _service.DeactiveAsync(id);
            return StatusCode(response.StatusCode, response);
        }
    }
}
