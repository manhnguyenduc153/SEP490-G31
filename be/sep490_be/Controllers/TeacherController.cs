using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
        private readonly UserManager<IdentityUser> _userManager;

        public TeacherController(ITeacherService service, UserManager<IdentityUser> userManager)
        {
            _service = service;
            _userManager = userManager;
        }

        // GET: api/Teacher
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] TeacherSearchDto searchDto)
        {
            var username = User.Identity?.Name;
            var hasViewPermission = User.Claims.Any(c => 
                c.Type.Equals("Permission", StringComparison.OrdinalIgnoreCase) && 
                c.Value.Equals(Permissions.Teacher.Teacher_View, StringComparison.OrdinalIgnoreCase));

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

        // GET: api/Teacher/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var username = User.Identity?.Name;
            var hasViewPermission = User.Claims.Any(c => 
                c.Type.Equals("Permission", StringComparison.OrdinalIgnoreCase) && 
                c.Value.Equals(Permissions.Teacher.Teacher_View, StringComparison.OrdinalIgnoreCase));

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
        public async Task<IActionResult> Edit(int id, [FromBody] TeacherSaveDto dto)
        {
            var username = User.Identity?.Name;
            var hasEditPermission = User.Claims.Any(c => 
                c.Type.Equals("Permission", StringComparison.OrdinalIgnoreCase) && 
                c.Value.Equals(Permissions.Teacher.Teacher_Edit, StringComparison.OrdinalIgnoreCase));

            var isEditingSelf = false;
            if (!string.IsNullOrEmpty(username))
            {
                var teacherResponse = await _service.GetByIdAsync(id);
                if (teacherResponse.StatusCode == 200 && teacherResponse.Data != null)
                {
                    var user = await _userManager.FindByNameAsync(username);
                    if (user != null && string.Equals(teacherResponse.Data.Email, user.Email, StringComparison.OrdinalIgnoreCase))
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

