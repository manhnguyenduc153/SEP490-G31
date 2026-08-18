using System.Collections.Generic;

namespace sep490_be.DTO.Class
{
    public class AutoScheduleSemesterResultDto
    {
        public List<ClassDto> Classes { get; set; } = new();
        public ScheduleReliabilityReportDto Reliability { get; set; } = new();
        // Only populated when the solver could not find any feasible schedule (Classes is empty in that case).
        // Structured as code + params rather than a pre-formatted sentence so the frontend can localize it.
        public List<InfeasibilityReasonDto>? InfeasibilityReasons { get; set; }
    }

    // Code identifies which diagnostic template the frontend should use (see backendMessages.infeasibilityReasons.*
    // in the i18n files); Params carries the interpolation values (course name, counts...) for that template.
    public class InfeasibilityReasonDto
    {
        public string Code { get; set; } = string.Empty;
        public Dictionary<string, string> Params { get; set; } = new();
    }

    public class ScheduleReliabilityReportDto
    {
        public string SolverStatus { get; set; } = string.Empty; // "Optimal" | "Feasible"
        public bool IsProvenOptimal { get; set; }

        public ScheduleCoverageDto Coverage { get; set; } = new();
        public List<HardConstraintCheckDto> HardConstraintChecks { get; set; } = new();
        public List<SoftMetricDto> SoftMetrics { get; set; } = new();
        public ScheduleDescriptiveStatsDto DescriptiveStats { get; set; } = new();
    }

    public class ScheduleCoverageDto
    {
        public int TotalRequested { get; set; }
        public int TotalScheduled { get; set; }
        public double ScheduledRatePercent { get; set; }
        public List<UnscheduledStudentDto> UnscheduledStudents { get; set; } = new();
    }

    public class UnscheduledStudentDto
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public int CourseId { get; set; }
        public string CourseName { get; set; } = string.Empty;
        // "NO_GROUP_CLASS" | "TIME_MISMATCH" — maps to backendMessages.unscheduledReasons.* for localization.
        public string ReasonCode { get; set; } = string.Empty;
    }

    // Independent post-hoc check for a hard constraint. ViolationCount should always be 0
    // if the CP-SAT model is formulated correctly — this exists as a defense-in-depth trust signal,
    // decoupled from the solver itself, so a model bug doesn't silently pass as "optimal".
    public class HardConstraintCheckDto
    {
        public string Name { get; set; } = string.Empty;
        public int ViolationCount { get; set; }
        public List<string> ViolationDetails { get; set; } = new();
    }

    // A soft-constraint metric expressed in its real business unit (buổi/tuần, %, chỗ ngồi...),
    // not normalized to an abstract score, so it reads the same way to a developer and to a school admin.
    public class SoftMetricDto
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayValue { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // "Good" | "Warning" | "Bad"
        public List<string> Details { get; set; } = new();
    }

    public class ScheduleDescriptiveStatsDto
    {
        public Dictionary<string, int> ClassesByGradeBand { get; set; } = new();
        public Dictionary<string, int> TeachersByGradeBandInUse { get; set; } = new();
        public double AvgClassSize { get; set; }
        public ClassSizeExtremeDto? LargestClass { get; set; }
        public ClassSizeExtremeDto? SmallestClass { get; set; }
        public double? AvgRoomCapacity { get; set; }
        public double? AvgRoomFillRatePercent { get; set; }
    }

    public class ClassSizeExtremeDto
    {
        public string ClassCode { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public int Size { get; set; }
    }
}
