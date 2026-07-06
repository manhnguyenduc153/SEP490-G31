using System.Threading.Tasks;
using sep490_be.DTO;
using sep490_be.DTO.User;

namespace sep490_be.Services.Interfaces
{
    public interface IUserService
    {
        Task<ApiResponse<PagingResponse<UserDto>>> GetAllAsync(UserSearchDto searchDto);
        Task<ApiResponse<UserDto>> GetByIdAsync(string id);
        Task<ApiResponse<UserDto>> CreateAsync(UserCreateDto dto);
        Task<ApiResponse<UserDto>> EditAsync(UserUpdateDto dto);
        Task<ApiResponse<bool>> DeleteAsync(string id);
        Task<ApiResponse<bool>> DeactiveAsync(string id);
    }
}

