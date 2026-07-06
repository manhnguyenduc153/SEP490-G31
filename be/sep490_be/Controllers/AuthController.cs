using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using sep490_be.DTO.Auth;
using sep490_be.Services.Interfaces;

namespace sep490_be.Controllers
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

        [HttpPost("RefreshToken")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto requestDto)
        {
            var response = await _authService.RefreshTokenAsync(requestDto);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost("Logout")]
        public async Task<IActionResult> Logout([FromBody] LogoutRequestDto requestDto)
        {
            var response = await _authService.LogoutAsync(requestDto);
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

        [HttpGet("GetAllRole")]
        public async Task<IActionResult> GetAllRole([FromQuery] RoleSearchDto searchDto)
        {
            var response = await _authService.GetAllRoleAsync(searchDto);
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

