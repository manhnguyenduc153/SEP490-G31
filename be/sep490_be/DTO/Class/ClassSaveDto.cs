using System;
using System.Collections.Generic;
using sep490_be.Helpers;

namespace sep490_be.DTO.Class
{
    public class ClassSaveDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Status { get; set; }
        public int Type { get; set; } = 0; // 0 = Offline, 1 = Online
        public string? Url { get; set; }
        public string? Description { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? CourseId { get; set; }
        public int? TeacherId { get; set; }
        public string? ScheduleDisplay { get; set; }
        public int? ExpectedLessons { get; set; }
        public string? WeeklySchedulesJson { get; set; }
        public bool AutoRefund { get; set; }
        public int? SemesterId { get; set; }
        /// <summary>
        /// List of students to enroll with their individual enroll type (0 = Offline, 1 = Online).
        /// </summary>
        public List<StudentEnrollDto> Students { get; set; } = new List<StudentEnrollDto>();
        public List<WeeklyScheduleDto> WeeklySchedules { get; set; } = new List<WeeklyScheduleDto>();
        public List<NewStudentDto> NewStudents { get; set; } = new List<NewStudentDto>();
        public string? NewTeacherEmail { get; set; }
        public string? NewTeacherName { get; set; }
        public string? NewCourseName { get; set; }

        /// <summary>
        /// 0 = Weekly (repeated across semester), 1 = SpecificSessions (custom per-session / monthly).
        /// </summary>
        public int ScheduleConfigMode { get; set; } = 0;
        public bool ForceOverride { get; set; } = false;
        public List<SpecificSessionScheduleDto> SpecificSchedules { get; set; } = new List<SpecificSessionScheduleDto>();
        /// <summary>
        /// Convenience helper: extract student IDs from Students list (for backward-compatible logic).
        /// </summary>
        public List<int> StudentIds => Students?.Select(s => s.StudentId).ToList() ?? new List<int>();

        public string TextSearch => StringHelper.GenerateTextSearch(Code, Name, Description, ScheduleDisplay);
    }

    /// <summary>
    /// Represents an individual scheduled session in specific session / monthly mode.
    /// </summary>
    public class SpecificSessionScheduleDto
    {
        public int? Id { get; set; }
        public int LessonNo { get; set; }
        public DateTime ScheduleDate { get; set; }
        public int? SlotId { get; set; }
        public int? SlotIndex { get; set; }
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
        public int? RoomId { get; set; }
        public int? TeacherId { get; set; }
    }

    /// <summary>
    /// Represents a student enrollment entry with an enroll type.
    /// </summary>
    public class StudentEnrollDto
    {
        public int StudentId { get; set; }
        /// <summary>0 = Offline, 1 = Online</summary>
        public int EnrollType { get; set; } = 0;
    }

    public class NewStudentDto
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
    }

    public class WeeklyScheduleDto
    {
        public int DayOfWeek { get; set; }
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public int? RoomId { get; set; }
    }
}

