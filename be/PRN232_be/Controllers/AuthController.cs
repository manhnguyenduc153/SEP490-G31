using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PRN232_be.DTO.Auth;
using PRN232_be.Services.Interfaces;

namespace PRN232_be.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("Login")]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            var response = await _authService.LoginAsync(loginDto);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost("Register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
        {
            var response = await _authService.RegisterAsync(registerDto);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost("CreateRole")]
        public async Task<IActionResult> CreateRole([FromBody] CreateRoleDto createRoleDto)
        {
            var response = await _authService.CreateRoleAsync(createRoleDto);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost("AssignRolePermissions")]
        public async Task<IActionResult> AssignRolePermissions([FromBody] AssignRolePermissionsDto assignRolePermissionsDto)
        {
            var response = await _authService.AssignRolePermissionsAsync(assignRolePermissionsDto);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost("AssignUserRole")]
        public async Task<IActionResult> AssignUserRole([FromQuery] string username, [FromQuery] string roleName)
        {
            var response = await _authService.AssignUserRoleAsync(username, roleName);
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("GetAllRoles")]
        public async Task<IActionResult> GetAllRoles()
        {
            var response = await _authService.GetAllRolesAsync();
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("GetAllPermissions")]
        public async Task<IActionResult> GetAllPermissions()
        {
            var response = await _authService.GetAllPermissionsAsync();
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("GetCurrentRoles")]
        [Authorize]
        public async Task<IActionResult> GetCurrentRoles()
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username))
            {
                return Unauthorized();
            }
            var response = await _authService.GetUserRolesAsync(username);
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("GetCurrentPermissions")]
        [Authorize]
        public async Task<IActionResult> GetCurrentPermissions()
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username))
            {
                return Unauthorized();
            }
            var response = await _authService.GetUserPermissionsAsync(username);
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("GetRolePermissions/{roleName}")]
        public async Task<IActionResult> GetRolePermissions(string roleName)
        {
            var response = await _authService.GetRolePermissionsAsync(roleName);
            return StatusCode(response.StatusCode, response);
        }
    }
}
