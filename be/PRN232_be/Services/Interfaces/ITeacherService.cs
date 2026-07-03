
using System.Threading.Tasks;
using PRN232_be.DTO;
using PRN232_be.DTO.Teacher;

namespace PRN232_be.Services.Interfaces
{
    public interface ITeacherService
    {
        Task<ApiResponse<PagingResponse<TeacherDto>>> GetAllAsync(TeacherSearchDto searchDto);
        Task<ApiResponse<TeacherDto>> GetByIdAsync(int id);
        Task<ApiResponse<TeacherDto>> CreateAsync(TeacherSaveDto dto);
        Task<ApiResponse<TeacherDto>> EditAsync(TeacherSaveDto dto);
        Task<ApiResponse<bool>> DeleteAsync(int id);
        Task<ApiResponse<bool>> DeactiveAsync(int id);
        Task<ApiResponse<List<TeacherDto>>> ImportAsync(List<TeacherSaveDto> dtos);
    }
}
