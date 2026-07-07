using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;
using PRN232_be.DTO.Exam;
using PRN232_be.DTO.Common;
using PRN232_be.DTO;
using System.Collections.Generic;
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

        // GET: api/Exam/5/all-attempts
        [HttpGet("{id}/all-attempts")]
        [HasPermission(Permissions.Exam.Exam_View)]
        public async Task<IActionResult> GetAttemptsByExam(int id)
        {
            var response = await _service.GetAttemptsByExamAsync(id);
            return StatusCode(response.StatusCode, response);
        }

        // GET: api/Exam/{id}/student-detail - for students to read exam without Exam_View permission
        [HttpGet("{id}/student-detail")]
        public async Task<IActionResult> GetStudentExamDetail(int id)
        {
            var response = await _service.GetByIdAsync(id);
            return StatusCode(response.StatusCode, response);
        }

        // GET: api/Exam/student
        [HttpGet("student")]
        public async Task<IActionResult> GetStudentExams()
        {
            var userEmailOrCode = GetCurrentUserEmailOrCode();
            if (string.IsNullOrEmpty(userEmailOrCode))
            {
                return BadRequest(ApiResponse<List<ExamDto>>.Fail("Không xác định được người dùng."));
            }
            var response = await _service.GetStudentExamsAsync(userEmailOrCode);
            return StatusCode(response.StatusCode, response);
        }

        // GET: api/Exam/5/attempts
        [HttpGet("{id}/attempts")]
        public async Task<IActionResult> GetStudentAttempts(int id)
        {
            var userEmailOrCode = GetCurrentUserEmailOrCode();
            if (string.IsNullOrEmpty(userEmailOrCode))
            {
                return BadRequest(ApiResponse<List<ExamAttemptDto>>.Fail("Không xác định được người dùng."));
            }
            var response = await _service.GetStudentAttemptsAsync(id, userEmailOrCode);
            return StatusCode(response.StatusCode, response);
        }

        // POST: api/Exam/5/start
        [HttpPost("{id}/start")]
        public async Task<IActionResult> StartAttempt(int id)
        {
            var userEmailOrCode = GetCurrentUserEmailOrCode();
            if (string.IsNullOrEmpty(userEmailOrCode))
            {
                return BadRequest(ApiResponse<ExamAttemptDto>.Fail("Không xác định được người dùng."));
            }
            var response = await _service.StartAttemptAsync(id, userEmailOrCode);
            return StatusCode(response.StatusCode, response);
        }

        // POST: api/Exam/5/submit
        [HttpPost("{id}/submit")]
        public async Task<IActionResult> SubmitAttempt(int id, [FromBody] ExamSubmitDto submitDto)
        {
            var userEmailOrCode = GetCurrentUserEmailOrCode();
            if (string.IsNullOrEmpty(userEmailOrCode))
            {
                return BadRequest(ApiResponse<ExamAttemptDto>.Fail("Không xác định được người dùng."));
            }
            var response = await _service.SubmitAttemptAsync(id, submitDto, userEmailOrCode);
            return StatusCode(response.StatusCode, response);
        }

        private string? GetCurrentUserEmailOrCode()
        {
            var identifier = User.Claims
                .Where(c =>
                    c.Type == System.Security.Claims.ClaimTypes.Email ||
                    c.Type == System.Security.Claims.ClaimTypes.Name ||
                    c.Type == System.Security.Claims.ClaimTypes.NameIdentifier ||
                    c.Type == "email" ||
                    c.Type == "sub" ||
                    c.Type == "unique_name" ||
                    c.Type == "preferred_username")
                .Select(c => c.Value)
                .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

            return identifier ?? User.Identity?.Name;
        }
    }
}
