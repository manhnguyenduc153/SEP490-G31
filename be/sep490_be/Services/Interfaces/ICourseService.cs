using sep490_be.DTO;
using sep490_be.DTO.Course;

namespace sep490_be.Services.Interfaces
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

