using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using sep490_be.DTO.Course;
using sep490_be.DTO.Common;
using sep490_be.Services.Interfaces;
using sep490_be.Helpers.Authorization;

namespace sep490_be.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CourseController : ControllerBase
    {
        private readonly ICourseService _service;

        public CourseController(ICourseService service)
        {
            _service = service;
        }

        // GET: api/Course
        [HttpGet]
        [HasPermission(Permissions.Course.Course_View)]
        public async Task<IActionResult> GetAll([FromQuery] CourseSearchDto searchDto)
        {
            var response = await _service.GetAllAsync(searchDto);
            return StatusCode(response.StatusCode, response);
        }

        // GET: api/Course/5
        [HttpGet("{id}")]
        [HasPermission(Permissions.Course.Course_View)]
        public async Task<IActionResult> GetById(int id)
        {
            var response = await _service.GetByIdAsync(id);
            return StatusCode(response.StatusCode, response);
        }

        // POST: api/Course
        [HttpPost]
        [HasPermission(Permissions.Course.Course_Create)]
        public async Task<IActionResult> Create([FromBody] CourseSaveDto dto)
        {
            var response = await _service.CreateAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        // PUT: api/Course/5
        [HttpPut("{id}")]
        [HasPermission(Permissions.Course.Course_Edit)]
        public async Task<IActionResult> Edit(int id, [FromBody] CourseSaveDto dto)
        {
            dto.Id = id;
            var response = await _service.EditAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        // DELETE: api/Course/5
        [HttpDelete("{id}")]
        [HasPermission(Permissions.Course.Course_Delete)]
        public async Task<IActionResult> Delete(int id)
        {
            var response = await _service.DeleteAsync(id);
            return StatusCode(response.StatusCode, response);
        }

        // POST: api/Course/5/deactive
        [HttpPost("{id}/deactive")]
        [HasPermission(Permissions.Course.Course_Delete)]
        public async Task<IActionResult> Deactive(int id)
        {
            var response = await _service.DeactiveAsync(id);
            return StatusCode(response.StatusCode, response);
        }
    }
}

