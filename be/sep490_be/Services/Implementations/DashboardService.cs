using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using sep490_be.DTO;
using sep490_be.DTO.Dashboard;
using sep490_be.Enums;
using sep490_be.Models;
using sep490_be.Services.Interfaces;

namespace sep490_be.Services.Implementations
{
    public class DashboardService : IDashboardService
    {
        private readonly ApplicationDbContext _dbContext;

        public DashboardService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<ApiResponse<DashboardDataDto>> GetDashboardDataAsync()
        {
            try
            {
                var result = new DashboardDataDto
                {
                    Metrics = await GetMetricsAsync(),
                    MonthlyEnrollments = await GetMonthlyEnrollmentsAsync(),
                    CoursePopularity = await GetCoursePopularityAsync(),
                    ClassStatusDistribution = await GetClassStatusDistributionAsync(),
                    RecentRegistrations = await GetRecentRegistrationsAsync(),
                    LowAttendanceAlerts = await GetLowAttendanceAlertsAsync(),
                    RoomUtilization = await GetRoomUtilizationAsync(),
                    TeacherWorkload = await GetTeacherWorkloadsAsync(),
                    GradingProgress = await GetGradingProgressAsync(),
                    ExamGradeDistribution = await GetExamGradeDistributionAsync()
                };

                return ApiResponse<DashboardDataDto>.Ok(result, "Dashboard data retrieved successfully.");
            }
            catch (Exception ex)
            {
                return ApiResponse<DashboardDataDto>.Fail(
                    $"Error retrieving dashboard data: {ex.Message}",
                    StatusCodes.Status500InternalServerError);
            }
        }

        // ── 1. Key Metrics ───────────────────────────────────────────────────
        private async Task<DashboardMetricsDto> GetMetricsAsync()
        {
            var totalStudents = await _dbContext.Set<Student>()
                .CountAsync(s => !s.IsDeleted && s.Status == (int)StudentStatus.Active);

            var totalClasses = await _dbContext.Set<Class>()
                .CountAsync(c => !c.IsDeleted &&
                    (c.Status == (int)ClassStatus.Planning || c.Status == (int)ClassStatus.Active));

            // Attendance rate: (Present + Late) / Total
            var totalAttendance = await _dbContext.Set<Attendance>()
                .CountAsync(a => !a.IsDeleted);

            var presentCount = await _dbContext.Set<Attendance>()
                .CountAsync(a => !a.IsDeleted &&
                    (a.Status == (int)AttendanceStatus.Present || a.Status == (int)AttendanceStatus.Late));

            var averageAttendanceRate = totalAttendance > 0
                ? Math.Round((double)presentCount / totalAttendance * 100, 1)
                : 0;

            var pendingRegistrations = await _dbContext.Set<StudentRegistration>()
                .CountAsync(r => r.Status == (int)StudentRegistrationStatus.Pending);

            return new DashboardMetricsDto
            {
                TotalStudents = totalStudents,
                TotalClasses = totalClasses,
                AverageAttendanceRate = averageAttendanceRate,
                PendingRegistrations = pendingRegistrations
            };
        }

        // ── 2. Monthly Enrollments (last 12 months) ─────────────────────────
        private async Task<List<MonthlyEnrollmentDto>> GetMonthlyEnrollmentsAsync()
        {
            var now = DateTime.UtcNow;
            var startDate = new DateTime(now.Year, now.Month, 1).AddMonths(-11);

            var enrollments = await _dbContext.Set<StudentClass>()
                .Where(sc => sc.EnrollDate != null && sc.EnrollDate >= startDate)
                .GroupBy(sc => new { sc.EnrollDate!.Value.Year, sc.EnrollDate!.Value.Month })
                .Select(g => new MonthlyEnrollmentDto
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Count = g.Count()
                })
                .OrderBy(e => e.Year).ThenBy(e => e.Month)
                .ToListAsync();

            // Fill in missing months with 0
            var monthNames = new[] { "", "Jan", "Feb", "Mar", "Apr", "May", "Jun",
                                      "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
            var result = new List<MonthlyEnrollmentDto>();
            for (int i = 0; i < 12; i++)
            {
                var date = startDate.AddMonths(i);
                var existing = enrollments.FirstOrDefault(e => e.Year == date.Year && e.Month == date.Month);
                result.Add(new MonthlyEnrollmentDto
                {
                    Year = date.Year,
                    Month = date.Month,
                    MonthLabel = monthNames[date.Month],
                    Count = existing?.Count ?? 0
                });
            }

            return result;
        }

        // ── 3. Course Popularity ─────────────────────────────────────────────
        private async Task<List<CoursePopularityDto>> GetCoursePopularityAsync()
        {
            var courseStudentCounts = await _dbContext.Set<StudentClass>()
                .Include(sc => sc.Class)
                    .ThenInclude(c => c.Course)
                .Where(sc => sc.Class != null && sc.Class.Course != null && !sc.Class.IsDeleted)
                .GroupBy(sc => sc.Class!.Course!.Name)
                .Select(g => new
                {
                    CourseName = g.Key,
                    StudentCount = g.Count()
                })
                .OrderByDescending(x => x.StudentCount)
                .ToListAsync();

            var total = courseStudentCounts.Sum(c => c.StudentCount);

            return courseStudentCounts.Select(c => new CoursePopularityDto
            {
                CourseName = c.CourseName,
                StudentCount = c.StudentCount,
                Percentage = total > 0 ? Math.Round((double)c.StudentCount / total * 100, 1) : 0
            }).ToList();
        }

        // ── 4. Class Status Distribution ─────────────────────────────────────
        private async Task<List<ClassStatusDistributionDto>> GetClassStatusDistributionAsync()
        {
            var statusGroups = await _dbContext.Set<Class>()
                .Where(c => !c.IsDeleted && c.Status != (int)ClassStatus.Cancelled)
                .GroupBy(c => c.Status)
                .Select(g => new
                {
                    Status = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            var statusNames = new Dictionary<int, string>
            {
                { (int)ClassStatus.Planning, "Chuẩn bị khai giảng" },
                { (int)ClassStatus.Active, "Đang diễn ra" },
                { (int)ClassStatus.Completed, "Đã hoàn thành" }
            };

            return statusGroups
                .Where(g => statusNames.ContainsKey(g.Status))
                .Select(g => new ClassStatusDistributionDto
                {
                    StatusName = statusNames[g.Status],
                    Count = g.Count
                })
                .OrderBy(d => d.StatusName)
                .ToList();
        }

        // ── 5. Recent Registrations (top 10 pending) ─────────────────────────
        private async Task<List<RecentRegistrationDto>> GetRecentRegistrationsAsync()
        {
            return await _dbContext.Set<StudentRegistration>()
                .Include(r => r.Student)
                .Include(r => r.Course)
                .Where(r => r.Status == (int)StudentRegistrationStatus.Pending)
                .OrderByDescending(r => r.Id)
                .Take(10)
                .Select(r => new RecentRegistrationDto
                {
                    Id = r.Id,
                    StudentName = r.Student.Name,
                    CourseName = r.Course.Name,
                    PreferredSlots = r.PreferredSlotsJson,
                    RegistrationDate = r.Student.CreatedAt
                })
                .ToListAsync();
        }

        // ── 6. Low Attendance Alerts (< 80%) ────────────────────────────────
        private async Task<List<LowAttendanceAlertDto>> GetLowAttendanceAlertsAsync()
        {
            // Get all attendance records for active classes, grouped by student + class
            var attendanceData = await _dbContext.Set<Attendance>()
                .Include(a => a.ClassSchedule)
                    .ThenInclude(cs => cs!.Class)
                .Include(a => a.Student)
                .Where(a => !a.IsDeleted
                    && a.ClassSchedule != null
                    && a.ClassSchedule.Class != null
                    && !a.ClassSchedule.Class.IsDeleted
                    && a.ClassSchedule.Class.Status == (int)ClassStatus.Active
                    && a.Student != null
                    && !a.Student.IsDeleted)
                .Select(a => new
                {
                    StudentId = a.StudentId!.Value,
                    StudentName = a.Student!.Name,
                    ClassName = a.ClassSchedule!.Class!.Name,
                    ClassId = a.ClassSchedule.ClassId!.Value,
                    a.Status,
                    ScheduleDate = a.ClassSchedule.ScheduleDate
                })
                .ToListAsync();

            // Group by student + class
            var grouped = attendanceData
                .GroupBy(a => new { a.StudentId, a.ClassId })
                .Select(g =>
                {
                    var total = g.Count();
                    var present = g.Count(a =>
                        a.Status == (int)AttendanceStatus.Present ||
                        a.Status == (int)AttendanceStatus.Late);
                    var rate = total > 0 ? Math.Round((double)present / total * 100, 1) : 100;

                    // Calculate consecutive absences (from latest schedule date backwards)
                    var orderedByDate = g
                        .OrderByDescending(a => a.ScheduleDate)
                        .ToList();
                    var consecutiveAbsences = 0;
                    foreach (var record in orderedByDate)
                    {
                        if (record.Status == (int)AttendanceStatus.Absent)
                            consecutiveAbsences++;
                        else
                            break;
                    }

                    return new LowAttendanceAlertDto
                    {
                        StudentId = g.Key.StudentId,
                        StudentName = g.First().StudentName,
                        ClassName = g.First().ClassName,
                        AttendanceRate = rate,
                        ConsecutiveAbsences = consecutiveAbsences,
                        Status = rate < 70 || consecutiveAbsences >= 3 ? "Critical" : "Warning"
                    };
                })
                .Where(a => a.AttendanceRate < 80)
                .OrderBy(a => a.AttendanceRate)
                .Take(10)
                .ToList();

            return grouped;
        }

        // ── 7. Room Utilization ─────────────────────────────────────────────
        private async Task<List<RoomUtilizationDto>> GetRoomUtilizationAsync()
        {
            var activeRooms = await _dbContext.Rooms
                .Where(r => !r.IsDeleted && r.Status == 1)
                .ToListAsync();

            var now = DateTime.Today;
            var startDate = now.AddDays(-15);
            var endDate = now.AddDays(15);

            var roomScheduleCounts = await _dbContext.ClassSchedules
                .Where(cs => !cs.IsDeleted && cs.RoomId.HasValue && cs.ScheduleDate >= startDate && cs.ScheduleDate <= endDate)
                .GroupBy(cs => cs.RoomId!.Value)
                .Select(g => new { RoomId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.RoomId, x => x.Count);

            var result = new List<RoomUtilizationDto>();
            foreach (var room in activeRooms)
            {
                var occupied = roomScheduleCounts.ContainsKey(room.Id) ? roomScheduleCounts[room.Id] : 0;
                double capacity = 120; // 30 slots per week * 4 weeks
                var rate = Math.Round((double)occupied / capacity * 100, 1);
                if (rate > 100) rate = 100;

                result.Add(new RoomUtilizationDto
                {
                    RoomId = room.Id,
                    RoomName = room.Name,
                    TotalSlots = (int)capacity,
                    OccupiedSlots = occupied,
                    UtilizationRate = rate
                });
            }

            return result.OrderByDescending(r => r.UtilizationRate).Take(10).ToList();
        }

        // ── 8. Teacher Workload ─────────────────────────────────────────────
        private async Task<List<TeacherWorkloadDto>> GetTeacherWorkloadsAsync()
        {
            var activeTeachers = await _dbContext.Teachers
                .Where(t => !t.IsDeleted && t.Status == 1)
                .ToListAsync();

            var now = DateTime.Today;
            var startDate = now.AddDays(-15);
            var endDate = now.AddDays(15);

            var teacherScheduleCounts = await _dbContext.ClassSchedules
                .Where(cs => !cs.IsDeleted && cs.TeacherId.HasValue && cs.ScheduleDate >= startDate && cs.ScheduleDate <= endDate)
                .GroupBy(cs => cs.TeacherId!.Value)
                .Select(g => new { TeacherId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.TeacherId, x => x.Count);

            var result = new List<TeacherWorkloadDto>();
            foreach (var teacher in activeTeachers)
            {
                var sessions = teacherScheduleCounts.ContainsKey(teacher.Id) ? teacherScheduleCounts[teacher.Id] : 0;
                result.Add(new TeacherWorkloadDto
                {
                    TeacherId = teacher.Id,
                    TeacherName = teacher.Name,
                    TeacherCode = teacher.Code,
                    TotalSessions = sessions
                });
            }

            return result.OrderByDescending(t => t.TotalSessions).Take(10).ToList();
        }

        // ── 9. Grading Progress ─────────────────────────────────────────────
        private async Task<GradingProgressDto> GetGradingProgressAsync()
        {
            var pendingHomeworks = await _dbContext.HomeworkSubmissions
                .CountAsync(s => !s.IsDeleted && s.Score == null);

            var pendingExams = await _dbContext.ExamAttempts
                .CountAsync(ea => !ea.IsDeleted && ea.SubmitTime != null && ea.Score == null);

            return new GradingProgressDto
            {
                PendingHomeworksCount = pendingHomeworks,
                PendingExamsCount = pendingExams
            };
        }

        // ── 10. Exam Grade Distribution ──────────────────────────────────────
        private async Task<List<ExamGradeDistributionDto>> GetExamGradeDistributionAsync()
        {
            var examScores = await _dbContext.ExamAttempts
                .Where(ea => !ea.IsDeleted && ea.Score.HasValue)
                .Select(ea => (double)ea.Score!.Value)
                .ToListAsync();

            var weakCount = examScores.Count(s => s < 5.0);
            var averageCount = examScores.Count(s => s >= 5.0 && s < 6.5);
            var goodCount = examScores.Count(s => s >= 6.5 && s < 8.0);
            var outstandingCount = examScores.Count(s => s >= 8.0);

            return new List<ExamGradeDistributionDto>
            {
                new() { ScoreBand = "< 5.0 (Weak)", StudentCount = weakCount },
                new() { ScoreBand = "5.0 - 6.5 (Average)", StudentCount = averageCount },
                new() { ScoreBand = "6.5 - 8.0 (Good)", StudentCount = goodCount },
                new() { ScoreBand = "8.0 - 10.0 (Excellent)", StudentCount = outstandingCount }
            };
        }
    }
}
