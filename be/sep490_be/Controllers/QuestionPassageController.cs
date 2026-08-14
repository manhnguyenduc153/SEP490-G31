using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using sep490_be.DTO.Common;
using sep490_be.DTO.QuestionPassage;
using sep490_be.Helpers.Authorization;
using sep490_be.Services.Interfaces;

namespace sep490_be.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class QuestionPassageController : ControllerBase
    {
        private readonly IQuestionPassageService _passageService;

        public QuestionPassageController(IQuestionPassageService passageService)
        {
            _passageService = passageService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] QuestionPassageSearchDto searchDto)
        {
            var result = await _passageService.GetAllAsync(searchDto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _passageService.GetByIdAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] QuestionPassageSaveDto dto)
        {
            var result = await _passageService.CreateAsync(dto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Edit(int id, [FromBody] QuestionPassageSaveDto dto)
        {
            dto.Id = id;
            var result = await _passageService.EditAsync(dto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _passageService.DeleteAsync(id);
            return StatusCode(result.StatusCode, result);
        }
    }
}
