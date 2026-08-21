using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using sep490_be.Common;
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
                .CountAsync(c => !c.IsDeleted && (c.Course == null || !c.Course.IsDeleted) &&
                    (c.Status == (int)ClassStatus.Planning || c.Status == (int)ClassStatus.Active));

            var activeTeachers = await _dbContext.Set<Teacher>()
                .CountAsync(t => !t.IsDeleted && t.Status == 1);

            // Attendance rate (for backward compatibility if needed)
            var totalAttendance = await _dbContext.Set<Attendance>()
                .CountAsync(a => !a.IsDeleted);

            var presentCount = await _dbContext.Set<Attendance>()
                .CountAsync(a => !a.IsDeleted &&
                    (a.Status == (int)AttendanceStatus.Present || a.Status == (int)AttendanceStatus.Late));

            var averageAttendanceRate = totalAttendance > 0
                ? Math.Round((double)presentCount / totalAttendance * 100, 1)
                : 0;

            var pendingRegistrations = await _dbContext.Set<StudentRegistration>()
                .CountAsync(r => (r.Course == null || !r.Course.IsDeleted) && (r.Student == null || !r.Student.IsDeleted) && r.Status == (int)StudentRegistrationStatus.Pending);

            return new DashboardMetricsDto
            {
                TotalStudents = totalStudents,
                TotalClasses = totalClasses,
                ActiveTeachers = activeTeachers,
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
                .Where(sc => sc.EnrollDate != null && sc.EnrollDate >= startDate
                    && sc.Student != null && !sc.Student.IsDeleted
                    && sc.Class != null && !sc.Class.IsDeleted
                    && sc.Class.Course != null && !sc.Class.Course.IsDeleted)
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
                .Where(sc => sc.Student != null && !sc.Student.IsDeleted
                    && sc.Class != null && !sc.Class.IsDeleted
                    && sc.Class.Course != null && !sc.Class.Course.IsDeleted)
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
                .Where(c => !c.IsDeleted && (c.Course == null || !c.Course.IsDeleted) && c.Status != (int)ClassStatus.Cancelled)
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
                .Where(r => r.Student != null && !r.Student.IsDeleted
                    && r.Course != null && !r.Course.IsDeleted
                    && r.Status == (int)StudentRegistrationStatus.Pending)
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
            return new List<LowAttendanceAlertDto>();
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
                .Where(cs => !cs.IsDeleted
                    && cs.RoomId.HasValue
                    && (cs.Class == null || (!cs.Class.IsDeleted && (cs.Class.Course == null || !cs.Class.Course.IsDeleted)))
                    && cs.ScheduleDate >= startDate && cs.ScheduleDate <= endDate)
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
                .Where(cs => !cs.IsDeleted
                    && cs.TeacherId.HasValue
                    && (cs.Class == null || (!cs.Class.IsDeleted && (cs.Class.Course == null || !cs.Class.Course.IsDeleted)))
                    && cs.ScheduleDate >= startDate && cs.ScheduleDate <= endDate)
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
                .CountAsync(s => !s.IsDeleted && s.Homework != null && !s.Homework.IsDeleted && s.Score == null);

            var pendingExams = await _dbContext.ExamAttempts
                .CountAsync(ea => !ea.IsDeleted && ea.Exam != null && !ea.Exam.IsDeleted && ea.SubmitTime != null && ea.Score == null);

            return new GradingProgressDto
            {
                PendingHomeworksCount = pendingHomeworks,
                PendingExamsCount = pendingExams
            };
        }

        // ── 10. Exam Grade Distribution ──────────────────────────────────────
        private async Task<List<ExamGradeDistributionDto>> GetExamGradeDistributionAsync()
        {
            var attempts = await _dbContext.ExamAttempts
                .Where(ea => !ea.IsDeleted && ea.Exam != null && !ea.Exam.IsDeleted && ea.Score.HasValue)
                .Include(ea => ea.Exam)
                    .ThenInclude(e => e.ExamQuestions)
                        .ThenInclude(eq => eq.Question)
                .ToListAsync();

            var examScores = attempts.Select(ea =>
            {
                var band = IeltsBandScale.ComputeAttemptBand(ea.Exam, ea.Score);
                if (band.HasValue) return (double)band.Value;

                var total = ea.Exam?.TotalScore ?? 10m;
                return total > 0 ? (double)Math.Max(0m, Math.Min(9m, ea.Score!.Value / total * 9m)) : 0.0;
            }).ToList();

            var weakCount = examScores.Count(s => s < 4.0);
            var averageCount = examScores.Count(s => s >= 4.0 && s < 5.5);
            var goodCount = examScores.Count(s => s >= 5.5 && s < 7.0);
            var outstandingCount = examScores.Count(s => s >= 7.0);

            return new List<ExamGradeDistributionDto>
            {
                new() { ScoreBand = "< 4.0 (Weak)", StudentCount = weakCount },
                new() { ScoreBand = "4.0 - 5.5 (Average)", StudentCount = averageCount },
                new() { ScoreBand = "5.5 - 7.0 (Good)", StudentCount = goodCount },
                new() { ScoreBand = "7.0 - 9.0 (Excellent)", StudentCount = outstandingCount }
            };
        }
    }
}
