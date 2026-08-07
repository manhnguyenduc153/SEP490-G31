
using System.Threading.Tasks;
using sep490_be.DTO;
using sep490_be.DTO.Teacher;

namespace sep490_be.Services.Interfaces
{
    public interface ITeacherService
    {
        Task<ApiResponse<PagingResponse<TeacherDto>>> GetAllAsync(TeacherSearchDto searchDto, string? username, bool hasViewPermission);
        Task<ApiResponse<TeacherDto>> GetByIdAsync(int id, string? username, bool hasViewPermission);
        Task<ApiResponse<TeacherDto>> CreateAsync(TeacherSaveDto dto);
        Task<ApiResponse<TeacherDto>> EditAsync(TeacherSaveDto dto, string? username, bool hasEditPermission);
        Task<ApiResponse<bool>> DeleteAsync(int id);
        Task<ApiResponse<bool>> DeactiveAsync(int id);
        Task<ApiResponse<List<TeacherDto>>> ImportAsync(List<TeacherSaveDto> dtos);
        Task<ApiResponse<bool>> BulkProvisionAccountsAsync(List<int> teacherIds);
    }
}

