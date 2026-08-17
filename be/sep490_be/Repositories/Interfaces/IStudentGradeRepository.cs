using sep490_be.Models;
using sep490_be.Repositories.Common;

namespace sep490_be.Repositories.Interfaces
{
    public interface IStudentGradeRepository : IBaseRepository<StudentGrade, ApplicationDbContext>
    {
        Task<(int Id, int? CourseId)?> GetClassInfoAsync(int classId);
        Task<List<GradeComponent>> GetComponentsAsync(int courseId);
        Task<List<int>> GetStudentClassIdsAsync(int classId);
        Task<List<StudentGradeOverride>> GetOverridesAsync(List<int> studentClassIds, List<int> componentIds);
        Task<List<sep490_be.Models.StudentClass>> GetStudentEnrollmentsAsync(int studentId);
        Task<Dictionary<int, decimal?>> GetStudentOverridesAsync(int studentClassId, List<int> componentIds);
        Task<decimal> CalculateAttendanceScoreAsync(int classId, int studentId);
        Task<Dictionary<string, decimal>> CalculateExamSkillScoresAsync(int classId, int studentId);
        Task<List<sep490_be.DTO.StudentGrade.MyGradeHomeworkDto>> GetHomeworkScoresAsync(int classId, int studentId);
        Task<List<sep490_be.DTO.StudentGrade.MyGradeExamDto>> GetExamScoresAsync(int classId, int studentId);
        Task<Student?> ResolveStudentByIdentifiersAsync(IEnumerable<string> identifiers, HashSet<string> lookupSet);
        Task<bool> IsParentOfStudentAsync(string email, int studentId);
        Task<bool> StudentExistsAsync(int studentId);
        Task<bool> CourseExistsAsync(int courseId);
        Task<List<GradeComponent>> GetExistingComponentsAsync(int courseId);
        Task EnsureDefaultComponentsAsync(int courseId);
        Task SaveCourseComponentsAsync(int courseId, List<GradeComponent> toAdd, List<GradeComponent> toUpdate, List<GradeComponent> toRemove);
        Task SaveOverridesAsync(List<StudentGradeOverride> toAdd, List<StudentGradeOverride> toUpdate, List<StudentGradeOverride> toRemove);
    }
}
