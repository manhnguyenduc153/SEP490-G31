using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PRN232_be.DTO.User;
using PRN232_be.DTO.Common;
using PRN232_be.Services.Interfaces;
using PRN232_be.Helpers.Authorization;
using System.Threading.Tasks;

namespace PRN232_be.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly IUserService _service;

        public UserController(IUserService service)
        {
            _service = service;
        }

        // GET: api/User
        [HttpGet]
        [HasPermission(Permissions.User.User_View)]
        public async Task<IActionResult> GetAll([FromQuery] UserSearchDto searchDto)
        {
            var response = await _service.GetAllAsync(searchDto);
            return StatusCode(response.StatusCode, response);
        }

        // GET: api/User/{id}
        [HttpGet("{id}")]
        [HasPermission(Permissions.User.User_View)]
        public async Task<IActionResult> GetById(string id)
        {
            var response = await _service.GetByIdAsync(id);
            return StatusCode(response.StatusCode, response);
        }

        // POST: api/User
        [HttpPost]
        [HasPermission(Permissions.User.User_Create)]
        public async Task<IActionResult> Create([FromBody] UserCreateDto dto)
        {
            var response = await _service.CreateAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        // PUT: api/User/{id}
        [HttpPut("{id}")]
        [HasPermission(Permissions.User.User_Edit)]
        public async Task<IActionResult> Edit(string id, [FromBody] UserUpdateDto dto)
        {
            dto.Id = id;
            var response = await _service.EditAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        // DELETE: api/User/{id}
        [HttpDelete("{id}")]
        [HasPermission(Permissions.User.User_Delete)]
        public async Task<IActionResult> Delete(string id)
        {
            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (currentUserId == id)
            {
                return StatusCode(Microsoft.AspNetCore.Http.StatusCodes.Status400BadRequest, 
                    PRN232_be.DTO.ApiResponse<bool>.Fail("ERR_CANNOT_DELETE_SELF", Microsoft.AspNetCore.Http.StatusCodes.Status400BadRequest));
            }
            var response = await _service.DeleteAsync(id);
            return StatusCode(response.StatusCode, response);
        }

        // POST: api/User/{id}/deactive
        [HttpPost("{id}/deactive")]
        [HasPermission(Permissions.User.User_Delete)]
        public async Task<IActionResult> Deactive(string id)
        {
            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (currentUserId == id)
            {
                return StatusCode(Microsoft.AspNetCore.Http.StatusCodes.Status400BadRequest, 
                    PRN232_be.DTO.ApiResponse<bool>.Fail("ERR_CANNOT_DEACTIVATE_SELF", Microsoft.AspNetCore.Http.StatusCodes.Status400BadRequest));
            }
            var response = await _service.DeactiveAsync(id);
            return StatusCode(response.StatusCode, response);
        }
    }
}
