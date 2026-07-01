using PRN232_be.DTO;
using PRN232_be.DTO.Student;

namespace PRN232_be.Services.Interfaces
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
    }
}
