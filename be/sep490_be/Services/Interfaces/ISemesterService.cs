using System.Collections.Generic;
using System.Threading.Tasks;
using sep490_be.DTO;
using sep490_be.DTO.Semester;
using sep490_be.DTO.Teacher;
using sep490_be.DTO.Student;

namespace sep490_be.Services.Interfaces
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
        Task<ApiResponse<StudentRegistrationDto>> CreateStudentRegistrationAsync(StudentRegistrationSaveDto dto);
        Task<ApiResponse<StudentRegistrationDto>> EditStudentRegistrationAsync(int id, StudentRegistrationSaveDto dto);
        Task<ApiResponse<bool>> DeleteStudentRegistrationAsync(int id);
    }
}

