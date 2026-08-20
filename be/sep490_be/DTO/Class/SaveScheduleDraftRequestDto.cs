using System.Collections.Generic;

namespace sep490_be.DTO.Class
{
    /// <summary>
    /// Request body for POST /api/Class/save-schedule-draft
    /// Sent by the FE after the admin reviews and approves the auto-generated draft schedule.
    /// </summary>
    public class SaveScheduleDraftRequestDto
    {
        public int SemesterId { get; set; }
        public List<ClassDraftSaveDto> Classes { get; set; } = new();
    }

    /// <summary>
    /// Represents one draft class to be persisted.
    /// </summary>
    public class ClassDraftSaveDto
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int CourseId { get; set; }
        public int TeacherId { get; set; }
        public int EnrollType { get; set; } = 0; // 0 = Offline, 1 = Online
        public List<WeeklyScheduleDto> WeeklySchedules { get; set; } = new();
        /// <summary>
        /// Specific individual session schedules (captures custom moved dates, slot overrides, per-session rooms/teachers).
        /// </summary>
        public List<SpecificSessionScheduleDto> SpecificSchedules { get; set; } = new();
        /// <summary>
        /// List of student IDs with their individual enroll type.
        /// </summary>
        public List<StudentEnrollDto> Students { get; set; } = new();
        public int ExpectedLessons { get; set; } = 30;
    }
}
