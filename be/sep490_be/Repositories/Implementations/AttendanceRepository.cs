using sep490_be.Models;
using sep490_be.Repositories.Common;
using sep490_be.Repositories.Interfaces;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using sep490_be.Enums;

namespace sep490_be.Repositories.Implementations
{
    public class AttendanceRepository : BaseRepository<Models.Attendance, ApplicationDbContext>, IAttendanceRepository
    {
        public AttendanceRepository(ApplicationDbContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
        {
        }

        public async Task<ClassSchedule?> GetScheduleWithAttendancesAsync(int scheduleId)
        {
            return await _dbContext.ClassSchedules
                .Include(cs => cs.Class)
                    .ThenInclude(c => c!.StudentClasses)
                        .ThenInclude(sc => sc!.Student)
                .FirstOrDefaultAsync(cs => cs.Id == scheduleId);
        }

        public async Task<ClassSchedule?> GetScheduleAsync(int scheduleId)
        {
            return await _dbContext.ClassSchedules
                .FirstOrDefaultAsync(cs => cs.Id == scheduleId);
        }

        public async Task<Class?> GetClassWithStudentClassesAsync(int classId)
        {
            return await _dbContext.Classes
                .Include(c => c.StudentClasses)
                    .ThenInclude(sc => sc!.Student)
                .FirstOrDefaultAsync(c => c.Id == classId);
        }

        public async Task<List<ClassSchedule>> GetSchedulesByClassIdAsync(int classId)
        {
            return await _dbContext.ClassSchedules
                .Where(cs => cs.ClassId == classId)
                .OrderBy(cs => cs.LessonNo)
                .ToListAsync();
        }

        public async Task<Student?> GetStudentByIdentifiersAsync(IEnumerable<string> identifiers, HashSet<string> lookupSet)
        {
            var lookup = identifiers.ToList();
            var student = await _dbContext.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(s =>
                    (s.Email != null && lookup.Contains(s.Email)) ||
                    (s.Code != null && lookup.Contains(s.Code)));

            if (student == null)
            {
                var candidates = await _dbContext.Students
                    .AsNoTracking()
                    .Where(s => s.Email != null || s.Code != null)
                    .ToListAsync();

                student = candidates.FirstOrDefault(s =>
                    (!string.IsNullOrWhiteSpace(s.Email) &&
                        (lookupSet.Contains(s.Email) || lookupSet.Contains(s.Email.Split('@')[0]))) ||
                    (!string.IsNullOrWhiteSpace(s.Code) && lookupSet.Contains(s.Code)));
            }
            return student;
        }

        public async Task<List<sep490_be.DTO.Attendance.MyAttendanceClassDto>> GetStudentClassAttendancesAsync(int studentId)
        {
            return await _dbContext.StudentClasses
                .AsNoTracking()
                .Where(sc => sc.StudentId == studentId &&
                             sc.Class.Status == 1 &&
                             !sc.Class.IsDeleted)
                .OrderBy(sc => sc.Class.Name)
                .Select(sc => new sep490_be.DTO.Attendance.MyAttendanceClassDto
                {
                    ClassId = sc.ClassId,
                    ClassCode = sc.Class.Code,
                    ClassName = sc.Class.Name,
                    CourseName = sc.Class.Course != null ? sc.Class.Course.Name : null,
                    TeacherName = sc.Class.Teacher != null ? sc.Class.Teacher.Name : null,
                    AttendedSessions = sc.Class.ClassSchedules
                        .Where(schedule => !schedule.IsDeleted)
                        .SelectMany(schedule => schedule.Attendances)
                        .Count(attendance => attendance.StudentId == studentId && attendance.Status != (int)AttendanceStatus.Absent),
                    AbsentSessions = sc.Class.ClassSchedules
                        .Where(schedule => !schedule.IsDeleted)
                        .SelectMany(schedule => schedule.Attendances)
                        .Count(attendance => attendance.StudentId == studentId && attendance.Status == (int)AttendanceStatus.Absent),
                    TotalSessions = sc.Class.ClassSchedules
                        .Count(schedule => !schedule.IsDeleted)
                })
                .ToListAsync();
        }

        public async Task<bool> IsStudentEnrolledInClassAsync(int studentId, int classId)
        {
            return await _dbContext.StudentClasses
                .AsNoTracking()
                .AnyAsync(sc => sc.StudentId == studentId &&
                                sc.ClassId == classId &&
                                sc.Class.Status == 1 &&
                                !sc.Class.IsDeleted);
        }

        public async Task<List<sep490_be.DTO.Attendance.MyAttendanceSessionDto>> GetStudentAttendanceSessionsAsync(int classId, int studentId)
        {
            var sessions = await _dbContext.ClassSchedules
                .AsNoTracking()
                .Where(schedule => schedule.ClassId == classId && !schedule.IsDeleted)
                .OrderBy(schedule => schedule.ScheduleDate)
                .ThenBy(schedule => schedule.LessonNo)
                .Select(schedule => new
                {
                    Schedule = schedule,
                    Attendance = schedule.Attendances
                        .Where(a => a.StudentId == studentId && !a.IsDeleted)
                        .OrderByDescending(a => a.UpdatedAt)
                        .FirstOrDefault()
                })
                .ToListAsync();

            return sessions.Select(x => new sep490_be.DTO.Attendance.MyAttendanceSessionDto
            {
                ScheduleId = x.Schedule.Id,
                LessonNo = x.Schedule.LessonNo ?? 0,
                Date = x.Schedule.ScheduleDate,
                Status = x.Attendance?.Status ?? -1,
                StatusName = x.Attendance == null
                    ? "NOT_MARKED"
                    : ((AttendanceStatus)x.Attendance.Status).GetStringValue(),
                Description = x.Attendance?.Description
            }).ToList();
        }
    }
}

