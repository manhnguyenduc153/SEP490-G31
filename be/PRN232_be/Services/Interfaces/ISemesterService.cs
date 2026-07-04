using System.Collections.Generic;
using System.Threading.Tasks;
using PRN232_be.DTO;
using PRN232_be.DTO.Semester;
using PRN232_be.DTO.Teacher;
using PRN232_be.DTO.Student;

namespace PRN232_be.Services.Interfaces
{
    public interface ISemesterService
    {
        Task<ApiResponse<List<SemesterDto>>> GetAllAsync();
        Task<ApiResponse<SemesterDto>> GetByIdAsync(int id);
        Task<ApiResponse<SemesterDto>> CreateAsync(SemesterSaveDto dto);
        Task<ApiResponse<SemesterDto>> EditAsync(SemesterSaveDto dto);
        Task<ApiResponse<bool>> DeleteAsync(int id);

        // Teacher Availability APIs
        Task<ApiResponse<List<TeacherAvailabilityDto>>> GetTeacherAvailabilitiesAsync(int semesterId, int teacherId);
        Task<ApiResponse<bool>> SaveTeacherAvailabilityAsync(TeacherAvailabilitySaveDto dto);

        // Student Registration APIs
        Task<ApiResponse<List<StudentRegistrationDto>>> GetStudentRegistrationsAsync(int semesterId);
        Task<ApiResponse<PagingResponse<StudentRegistrationDto>>> GetStudentRegistrationsPagedAsync(
            int semesterId, string? keyword, int? courseId, int? status, int pageIndex, int pageSize);
        Task<ApiResponse<List<StudentRegistrationDto>>> ImportStudentRegistrationsAsync(List<StudentRegistrationSaveDto> dtos);
    }
}
