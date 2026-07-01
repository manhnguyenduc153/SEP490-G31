using System.Threading.Tasks;
using PRN232_be.DTO;
using PRN232_be.DTO.User;

namespace PRN232_be.Services.Interfaces
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
