using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN232_be.DTO.Common;
using PRN232_be.DTO.ParentStudent;
using PRN232_be.Helpers.Authorization;
using PRN232_be.Services.Interfaces;

namespace PRN232_be.Controllers
{
    /// <summary>
    /// API CRUD phụ huynh của học sinh.
    /// Khi tạo mới → tự động tạo account IdentityUser với role "Parent".
    /// </summary>
    [Route("api/parent-student")]
    [ApiController]
    [Authorize]
    public class ParentStudentController : ControllerBase
    {
        private readonly IParentStudentService _service;

        public ParentStudentController(IParentStudentService service)
        {
            _service = service;
        }

        // GET: api/parent-student?studentId=5&pageIndex=1&pageSize=10
        [HttpGet]
        [HasPermission(Permissions.ParentStudent.ParentStudent_View)]
        public async Task<IActionResult> GetAll([FromQuery] ParentStudentSearchDto searchDto)
        {
            var response = await _service.GetAllAsync(searchDto);
            return StatusCode(response.StatusCode, response);
        }

        // GET: api/parent-student/5
        [HttpGet("{id}")]
        [HasPermission(Permissions.ParentStudent.ParentStudent_View)]
        public async Task<IActionResult> GetById(int id)
        {
            var response = await _service.GetByIdAsync(id);
            return StatusCode(response.StatusCode, response);
        }

        // POST: api/parent-student
        // Body: { studentId, code, name, email, parentPhone, relationship, status }
        // → Tự động tạo IdentityUser với role "Parent", password mặc định "Parent@123456"
        [HttpPost]
        [HasPermission(Permissions.ParentStudent.ParentStudent_Create)]
        public async Task<IActionResult> Create([FromBody] ParentStudentSaveDto dto)
        {
            var response = await _service.CreateAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        // PUT: api/parent-student/5
        // Cập nhật thông tin phụ huynh (không thay đổi email/account)
        [HttpPut("{id}")]
        [HasPermission(Permissions.ParentStudent.ParentStudent_Edit)]
        public async Task<IActionResult> Edit(int id, [FromBody] ParentStudentSaveDto dto)
        {
            dto.Id = id;
            var response = await _service.EditAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        // DELETE: api/parent-student/5
        // Soft-delete ParentStudent + Lock IdentityUser tương ứng
        [HttpDelete("{id}")]
        [HasPermission(Permissions.ParentStudent.ParentStudent_Delete)]
        public async Task<IActionResult> Delete(int id)
        {
            var response = await _service.DeleteAsync(id);
            return StatusCode(response.StatusCode, response);
        }

        // POST: api/parent-student/5/deactive
        [HttpPost("{id}/deactive")]
        [HasPermission(Permissions.ParentStudent.ParentStudent_Delete)]
        public async Task<IActionResult> Deactive(int id)
        {
            var response = await _service.DeactiveAsync(id);
            return StatusCode(response.StatusCode, response);
        }
    }
}
