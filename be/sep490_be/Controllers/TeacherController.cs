using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using sep490_be.DTO.Teacher;
using sep490_be.DTO.Common;
using sep490_be.Services.Interfaces;
using sep490_be.Helpers.Authorization;

namespace sep490_be.Controllers
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

        // POST: api/Teacher/import
        [HttpPost("import")]
        [HasPermission(Permissions.Teacher.Teacher_Create)]
        public async Task<IActionResult> Import([FromBody] List<TeacherSaveDto> dtos)
        {
            var response = await _service.ImportAsync(dtos);
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

        // POST: api/Teacher/provision-accounts
        [HttpPost("provision-accounts")]
        [HasPermission(Permissions.Teacher.Teacher_Create)]
        public async Task<IActionResult> ProvisionAccounts([FromBody] List<int> teacherIds)
        {
            var response = await _service.BulkProvisionAccountsAsync(teacherIds);
            return StatusCode(response.StatusCode, response);
        }
    }
}

