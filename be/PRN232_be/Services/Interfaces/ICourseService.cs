using PRN232_be.DTO;
using PRN232_be.DTO.Course;

namespace PRN232_be.Services.Interfaces
{
    public interface ICourseService
    {
        Task<ApiResponse<PagingResponse<CourseDto>>> GetAllAsync(CourseSearchDto searchDto);
        Task<ApiResponse<CourseDto>> GetByIdAsync(int id);
        Task<ApiResponse<CourseDto>> CreateAsync(CourseSaveDto dto);
        Task<ApiResponse<CourseDto>> EditAsync(CourseSaveDto dto);
        Task<ApiResponse<bool>> DeleteAsync(int id);
        Task<ApiResponse<bool>> DeactiveAsync(int id);
    }
}
