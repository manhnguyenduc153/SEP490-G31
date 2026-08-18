using System;

namespace sep490_be.DTO.Teacher
{
    public class AvailableTeacherFilterDto
    {
        public int? CourseId { get; set; }
        public int? SemesterId { get; set; }
        public int? DayOfWeek { get; set; } // 0 = Sunday, 1 = Monday, ..., 6 = Saturday
        public int? SlotIndex { get; set; } // 0..4 (FixedTimeSlot index)
        public DateTime? Date { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? ExcludeScheduleId { get; set; }
        public int? ExcludeClassId { get; set; }
        public string? WeeklySchedulesJson { get; set; }
    }
}
