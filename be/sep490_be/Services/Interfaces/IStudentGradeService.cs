using sep490_be.DTO;
using sep490_be.DTO.StudentGrade;

namespace sep490_be.Services.Interfaces
{
    public interface IStudentGradeService
    {
        Task<ApiResponse<ClassGradeSettingsDto>> GetSettingsAsync(int classId);
        Task<ApiResponse<List<MyGradeClassDto>>> GetMyGradesAsync(IEnumerable<string> identifiers);
        Task<ApiResponse<List<GradeComponentDto>>> GetCourseComponentsAsync(int courseId);
        Task<ApiResponse<List<GradeComponentDto>>> SaveCourseComponentsAsync(int courseId, ClassGradeComponentsSaveDto dto);
        Task<ApiResponse<List<StudentGradeOverrideDto>>> SaveOverridesAsync(int classId, StudentGradeOverridesSaveDto dto);
        Task<ApiResponse<List<MyGradeClassDto>>> GetChildGradesAsync(string username, int studentId);
    }
}
