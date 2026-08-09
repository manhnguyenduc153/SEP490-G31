using sep490_be.Models;
using sep490_be.Repositories.Common;

namespace sep490_be.Repositories.Interfaces
{
    public interface IAttendanceRepository : IBaseRepository<Models.Attendance, ApplicationDbContext>
    {
        Task<ClassSchedule?> GetScheduleWithAttendancesAsync(int scheduleId);
        Task<ClassSchedule?> GetScheduleAsync(int scheduleId);
        Task<Class?> GetClassWithStudentClassesAsync(int classId);
        Task<List<ClassSchedule>> GetSchedulesByClassIdAsync(int classId);
        Task<Student?> GetStudentByIdentifiersAsync(IEnumerable<string> identifiers, HashSet<string> lookupSet);
        Task<List<sep490_be.DTO.Attendance.MyAttendanceClassDto>> GetStudentClassAttendancesAsync(int studentId);
        Task<bool> IsStudentEnrolledInClassAsync(int studentId, int classId);
        Task<List<sep490_be.DTO.Attendance.MyAttendanceSessionDto>> GetStudentAttendanceSessionsAsync(int classId, int studentId);
    }
}

