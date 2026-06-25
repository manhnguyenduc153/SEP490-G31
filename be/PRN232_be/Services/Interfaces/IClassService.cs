using PRN232_be.DTO;
using PRN232_be.DTO.Class;

namespace PRN232_be.Services.Interfaces
{
    public interface IClassService
    {
        Task<ApiResponse<PagingResponse<ClassDto>>> GetAllAsync(ClassSearchDto searchDto);
        Task<ApiResponse<ClassDto>> GetByIdAsync(int id);
        Task<ApiResponse<ClassDto>> CreateAsync(ClassSaveDto dto);
        Task<ApiResponse<ClassDto>> EditAsync(ClassSaveDto dto);
        Task<ApiResponse<bool>> DeleteAsync(int id);
        Task<ApiResponse<bool>> DeactiveAsync(int id);
    }
}
