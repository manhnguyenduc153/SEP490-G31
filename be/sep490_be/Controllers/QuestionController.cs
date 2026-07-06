using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;
using sep490_be.DTO.Question;
using sep490_be.DTO.Common;
using sep490_be.Services.Interfaces;
using sep490_be.Helpers.Authorization;

namespace sep490_be.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class QuestionController : ControllerBase
    {
        private readonly IQuestionService _service;

        public QuestionController(IQuestionService service)
        {
            _service = service;
        }

        // GET: api/Question
        [HttpGet]
        [HasPermission(Permissions.Question.Question_View)]
        public async Task<IActionResult> GetAll([FromQuery] QuestionSearchDto searchDto)
        {
            var response = await _service.GetAllAsync(searchDto);
            return StatusCode(response.StatusCode, response);
        }

        // GET: api/Question/5
        [HttpGet("{id}")]
        [HasPermission(Permissions.Question.Question_View)]
        public async Task<IActionResult> GetById(int id)
        {
            var response = await _service.GetByIdAsync(id);
            return StatusCode(response.StatusCode, response);
        }

        // POST: api/Question
        [HttpPost]
        [HasPermission(Permissions.Question.Question_Create)]
        public async Task<IActionResult> Create([FromBody] QuestionSaveDto dto)
        {
            var response = await _service.CreateAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        // PUT: api/Question/5
        [HttpPut("{id}")]
        [HasPermission(Permissions.Question.Question_Edit)]
        public async Task<IActionResult> Edit(int id, [FromBody] QuestionSaveDto dto)
        {
            dto.Id = id;
            var response = await _service.EditAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        // DELETE: api/Question/5
        [HttpDelete("{id}")]
        [HasPermission(Permissions.Question.Question_Delete)]
        public async Task<IActionResult> Delete(int id)
        {
            var response = await _service.DeleteAsync(id);
            return StatusCode(response.StatusCode, response);
        }

        // POST: api/Question/5/deactive
        [HttpPost("{id}/deactive")]
        [HasPermission(Permissions.Question.Question_Delete)]
        public async Task<IActionResult> Deactive(int id)
        {
            var response = await _service.DeactiveAsync(id);
            return StatusCode(response.StatusCode, response);
        }

        // POST: api/Question/import
        [HttpPost("import")]
        [HasPermission(Permissions.Question.Question_Create)]
        public async Task<IActionResult> Import([FromBody] List<QuestionSaveDto> dtos)
        {
            var response = await _service.ImportAsync(dtos);
            return StatusCode(response.StatusCode, response);
        }
    }
}

