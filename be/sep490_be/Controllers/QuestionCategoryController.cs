using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using sep490_be.DTO.QuestionCategory;
using sep490_be.DTO.Common;
using sep490_be.Services.Interfaces;
using sep490_be.Helpers.Authorization;

namespace sep490_be.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class QuestionCategoryController : ControllerBase
    {
        private readonly IQuestionCategoryService _service;

        public QuestionCategoryController(IQuestionCategoryService service)
        {
            _service = service;
        }

        // GET: api/QuestionCategory
        [HttpGet]
        [HasPermission(Permissions.QuestionCategory.QuestionCategory_View)]
        public async Task<IActionResult> GetAll([FromQuery] QuestionCategorySearchDto searchDto)
        {
            var response = await _service.GetAllAsync(searchDto);
            return StatusCode(response.StatusCode, response);
        }

        // GET: api/QuestionCategory/5
        [HttpGet("{id}")]
        [HasPermission(Permissions.QuestionCategory.QuestionCategory_View)]
        public async Task<IActionResult> GetById(int id)
        {
            var response = await _service.GetByIdAsync(id);
            return StatusCode(response.StatusCode, response);
        }

        // POST: api/QuestionCategory
        [HttpPost]
        [HasPermission(Permissions.QuestionCategory.QuestionCategory_Create)]
        public async Task<IActionResult> Create([FromBody] QuestionCategorySaveDto dto)
        {
            var response = await _service.CreateAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        // PUT: api/QuestionCategory/5
        [HttpPut("{id}")]
        [HasPermission(Permissions.QuestionCategory.QuestionCategory_Edit)]
        public async Task<IActionResult> Edit(int id, [FromBody] QuestionCategorySaveDto dto)
        {
            dto.Id = id;
            var response = await _service.EditAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        // DELETE: api/QuestionCategory/5
        [HttpDelete("{id}")]
        [HasPermission(Permissions.QuestionCategory.QuestionCategory_Delete)]
        public async Task<IActionResult> Delete(int id)
        {
            var response = await _service.DeleteAsync(id);
            return StatusCode(response.StatusCode, response);
        }

        // POST: api/QuestionCategory/5/deactive
        [HttpPost("{id}/deactive")]
        [HasPermission(Permissions.QuestionCategory.QuestionCategory_Delete)]
        public async Task<IActionResult> Deactive(int id)
        {
            var response = await _service.DeactiveAsync(id);
            return StatusCode(response.StatusCode, response);
        }
    }
}

