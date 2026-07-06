using sep490_be.DTO;
using sep490_be.DTO.Student;

namespace sep490_be.Services.Interfaces
{
    public interface IStudentService
    {
        Task<ApiResponse<PagingResponse<StudentDto>>> GetAllAsync(StudentSearchDto searchDto);
        Task<ApiResponse<StudentDto>> GetByIdAsync(int id);
        Task<ApiResponse<StudentDto>> CreateAsync(StudentSaveDto dto);
        Task<ApiResponse<StudentDto>> EditAsync(StudentSaveDto dto);
        Task<ApiResponse<bool>> DeleteAsync(int id);
        Task<ApiResponse<bool>> DeactiveAsync(int id);
        Task<ApiResponse<Dictionary<string, int>>> CheckEmailsAsync(List<string> emails);
        Task<ApiResponse<List<StudentDto>>> ImportAsync(List<StudentSaveDto> dtos);
        Task<ApiResponse<bool>> BulkProvisionAccountsAsync(List<int> studentIds);
    }
}

