using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PRN232_be.DTO.Class;
using PRN232_be.DTO.Common;
using PRN232_be.Services.Interfaces;
using PRN232_be.Helpers.Authorization;

namespace PRN232_be.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ClassController : ControllerBase
    {
        private readonly IClassService _service;

        public ClassController(IClassService service)
        {
            _service = service;
        }

        // GET: api/Class
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] ClassSearchDto searchDto)
        {
            var response = await _service.GetAllAsync(searchDto);
            return StatusCode(response.StatusCode, response);
        }

        // GET: api/Class/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var response = await _service.GetByIdAsync(id);
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
    }
}
