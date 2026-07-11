namespace sep490_be.DTO.Dashboard
{
    // ── Root DTO ──────────────────────────────────────────────────────────────
    public class DashboardDataDto
    {
        public DashboardMetricsDto Metrics { get; set; } = new();
        public List<MonthlyEnrollmentDto> MonthlyEnrollments { get; set; } = new();
        public List<CoursePopularityDto> CoursePopularity { get; set; } = new();
        public List<ClassStatusDistributionDto> ClassStatusDistribution { get; set; } = new();
        public List<RecentRegistrationDto> RecentRegistrations { get; set; } = new();
        public List<LowAttendanceAlertDto> LowAttendanceAlerts { get; set; } = new();
    }

    // ── Key Metrics ──────────────────────────────────────────────────────────
    public class DashboardMetricsDto
    {
        public int TotalStudents { get; set; }
        public int TotalClasses { get; set; }
        public double AverageAttendanceRate { get; set; }
        public int PendingRegistrations { get; set; }
    }

    // ── Monthly Enrollment ───────────────────────────────────────────────────
    public class MonthlyEnrollmentDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthLabel { get; set; } = string.Empty; // e.g. "Jan", "Feb"
        public int Count { get; set; }
    }

    // ── Course Popularity ────────────────────────────────────────────────────
    public class CoursePopularityDto
    {
        public string CourseName { get; set; } = string.Empty;
        public int StudentCount { get; set; }
        public double Percentage { get; set; }
    }

    // ── Class Status Distribution ────────────────────────────────────────────
    public class ClassStatusDistributionDto
    {
        public string StatusName { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    // ── Recent Registration ──────────────────────────────────────────────────
    public class RecentRegistrationDto
    {
        public int Id { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string CourseName { get; set; } = string.Empty;
        public string PreferredSlots { get; set; } = string.Empty;
        public DateTime RegistrationDate { get; set; }
    }

    // ── Low Attendance Alert ─────────────────────────────────────────────────
    public class LowAttendanceAlertDto
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public double AttendanceRate { get; set; }
        public int ConsecutiveAbsences { get; set; }
        public string Status { get; set; } = string.Empty; // "Warning" or "Critical"
    }
}
