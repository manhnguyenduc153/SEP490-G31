using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using sep490_be.DTO.Common;
using sep490_be.DTO.ParentStudent;
using sep490_be.Helpers.Authorization;
using sep490_be.Services.Interfaces;

namespace sep490_be.Controllers
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
        private readonly UserManager<IdentityUser> _userManager;

        public ParentStudentController(IParentStudentService service, UserManager<IdentityUser> userManager)
        {
            _service = service;
            _userManager = userManager;
        }

        // GET: api/parent-student?studentId=5&pageIndex=1&pageSize=10
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] ParentStudentSearchDto searchDto)
        {
            var username = User.Identity?.Name;
            var hasViewPermission = User.Claims.Any(c => 
                c.Type.Equals("Permission", StringComparison.OrdinalIgnoreCase) && 
                c.Value.Equals(Permissions.ParentStudent.ParentStudent_View, StringComparison.OrdinalIgnoreCase));

            if (!hasViewPermission)
            {
                if (string.IsNullOrEmpty(username))
                {
                    return Forbid();
                }

                var user = await _userManager.FindByNameAsync(username);
                if (user == null)
                {
                    return Forbid();
                }

                var isSearchingSelf = !string.IsNullOrEmpty(searchDto.Keyword) && 
                    (string.Equals(searchDto.Keyword, user.UserName, StringComparison.OrdinalIgnoreCase) || 
                     string.Equals(searchDto.Keyword, user.Email, StringComparison.OrdinalIgnoreCase));

                if (!isSearchingSelf)
                {
                    if (string.IsNullOrEmpty(searchDto.Keyword))
                    {
                        searchDto.Keyword = user.Email;
                    }
                    else
                    {
                        return Forbid();
                    }
                }
                else
                {
                    searchDto.Keyword = user.Email;
                }
            }

            var response = await _service.GetAllAsync(searchDto);
            return StatusCode(response.StatusCode, response);
        }

        // GET: api/parent-student/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var username = User.Identity?.Name;
            var hasViewPermission = User.Claims.Any(c => 
                c.Type.Equals("Permission", StringComparison.OrdinalIgnoreCase) && 
                c.Value.Equals(Permissions.ParentStudent.ParentStudent_View, StringComparison.OrdinalIgnoreCase));

            var isViewingSelf = false;
            var response = await _service.GetByIdAsync(id);
            if (response.StatusCode == 200 && response.Data != null && !string.IsNullOrEmpty(username))
            {
                var user = await _userManager.FindByNameAsync(username);
                if (user != null && string.Equals(response.Data.Email, user.Email, StringComparison.OrdinalIgnoreCase))
                {
                    isViewingSelf = true;
                }
            }

            if (!hasViewPermission && !isViewingSelf)
            {
                return Forbid();
            }

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
        public async Task<IActionResult> Edit(int id, [FromBody] ParentStudentSaveDto dto)
        {
            var username = User.Identity?.Name;
            var hasEditPermission = User.Claims.Any(c => 
                c.Type.Equals("Permission", StringComparison.OrdinalIgnoreCase) && 
                c.Value.Equals(Permissions.ParentStudent.ParentStudent_Edit, StringComparison.OrdinalIgnoreCase));

            var isEditingSelf = false;
            if (!string.IsNullOrEmpty(username))
            {
                var parentResponse = await _service.GetByIdAsync(id);
                if (parentResponse.StatusCode == 200 && parentResponse.Data != null)
                {
                    var user = await _userManager.FindByNameAsync(username);
                    if (user != null && string.Equals(parentResponse.Data.Email, user.Email, StringComparison.OrdinalIgnoreCase))
                    {
                        isEditingSelf = true;
                    }
                }
            }

            if (!hasEditPermission && !isEditingSelf)
            {
                return Forbid();
            }

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

