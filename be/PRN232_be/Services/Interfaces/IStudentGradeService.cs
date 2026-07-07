using PRN232_be.DTO;
using PRN232_be.DTO.StudentGrade;

namespace PRN232_be.Services.Interfaces
{
    public interface IStudentGradeService
    {
        Task<ApiResponse<ClassGradeSettingsDto>> GetSettingsAsync(int classId);
        Task<ApiResponse<List<GradeComponentDto>>> GetCourseComponentsAsync(int courseId);
        Task<ApiResponse<List<GradeComponentDto>>> SaveCourseComponentsAsync(int courseId, ClassGradeComponentsSaveDto dto);
        Task<ApiResponse<List<StudentGradeOverrideDto>>> SaveOverridesAsync(int classId, StudentGradeOverridesSaveDto dto);
    }
}
