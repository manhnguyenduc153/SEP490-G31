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
        /// Convenience helper: extract student IDs from Students list (for backward-compatible logic).
        /// </summary>
        public List<int> StudentIds => Students.Select(s => s.StudentId).ToList();

        public string TextSearch => StringHelper.GenerateTextSearch(Code, Name, Description, ScheduleDisplay);
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

