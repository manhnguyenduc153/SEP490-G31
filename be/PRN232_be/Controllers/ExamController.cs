using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;
using PRN232_be.DTO.Exam;
using PRN232_be.DTO.Common;
using PRN232_be.Services.Interfaces;
using PRN232_be.Helpers.Authorization;

namespace PRN232_be.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ExamController : ControllerBase
    {
        private readonly IExamService _service;

        public ExamController(IExamService service)
        {
            _service = service;
        }

        // GET: api/Exam
        [HttpGet]
        [HasPermission(Permissions.Exam.Exam_View)]
        public async Task<IActionResult> GetAll([FromQuery] ExamSearchDto searchDto)
        {
            var response = await _service.GetAllAsync(searchDto);
            return StatusCode(response.StatusCode, response);
        }

        // GET: api/Exam/5
        [HttpGet("{id}")]
        [HasPermission(Permissions.Exam.Exam_View)]
        public async Task<IActionResult> GetById(int id)
        {
            var response = await _service.GetByIdAsync(id);
            return StatusCode(response.StatusCode, response);
        }

        // POST: api/Exam
        [HttpPost]
        [HasPermission(Permissions.Exam.Exam_Create)]
        public async Task<IActionResult> Create([FromBody] ExamSaveDto dto)
        {
            var response = await _service.CreateAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        // PUT: api/Exam/5
        [HttpPut("{id}")]
        [HasPermission(Permissions.Exam.Exam_Edit)]
        public async Task<IActionResult> Edit(int id, [FromBody] ExamSaveDto dto)
        {
            dto.Id = id;
            var response = await _service.EditAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        // DELETE: api/Exam/5
        [HttpDelete("{id}")]
        [HasPermission(Permissions.Exam.Exam_Delete)]
        public async Task<IActionResult> Delete(int id)
        {
            var response = await _service.DeleteAsync(id);
            return StatusCode(response.StatusCode, response);
        }

        // POST: api/Exam/5/copy
        [HttpPost("{id}/copy")]
        [HasPermission(Permissions.Exam.Exam_Create)]
        public async Task<IActionResult> Copy(int id)
        {
            var response = await _service.CopyAsync(id);
            return StatusCode(response.StatusCode, response);
        }
    }
}
