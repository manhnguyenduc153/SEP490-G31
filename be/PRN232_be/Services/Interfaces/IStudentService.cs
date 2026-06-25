using PRN232_be.DTO;
using PRN232_be.DTO.Student;

namespace PRN232_be.Services.Interfaces
{
    public interface IStudentService
    {
        Task<ApiResponse<PagingResponse<StudentDto>>> GetAllAsync(StudentSearchDto searchDto);
        Task<ApiResponse<StudentDto>> GetByIdAsync(int id);
    }
}
