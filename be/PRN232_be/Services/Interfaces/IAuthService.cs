using PRN232_be.DTO;
using PRN232_be.DTO.Auth;

namespace PRN232_be.Services.Interfaces
{
    public interface IAuthService
    {
        Task<ApiResponse<TokenResponseDto>> LoginAsync(LoginDto loginDto);
        Task<ApiResponse<bool>> RegisterAsync(RegisterDto registerDto);
        Task<ApiResponse<bool>> CreateRoleAsync(CreateRoleDto createRoleDto);
        Task<ApiResponse<bool>> AssignRolePermissionsAsync(AssignRolePermissionsDto assignRolePermissionsDto);
        Task<ApiResponse<bool>> AssignUserRoleAsync(string username, string roleName);
        Task<ApiResponse<List<string>>> GetAllRolesAsync();
        Task<ApiResponse<PagingResponse<RoleDto>>> GetAllRoleAsync(RoleSearchDto searchDto);
        Task<ApiResponse<List<string>>> GetAllPermissionsAsync();
        Task<ApiResponse<List<string>>> GetUserRolesAsync(string username);
        Task<ApiResponse<List<string>>> GetUserPermissionsAsync(string username);
        Task<ApiResponse<List<string>>> GetRolePermissionsAsync(string roleName);
        Task<ApiResponse<TokenResponseDto>> RefreshTokenAsync(RefreshTokenRequestDto requestDto);
        Task<ApiResponse<bool>> LogoutAsync(LogoutRequestDto requestDto);
    }
}
