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
        public List<int> StudentIds { get; set; } = new List<int>();
        public List<WeeklyScheduleDto> WeeklySchedules { get; set; } = new List<WeeklyScheduleDto>();
        public List<NewStudentDto> NewStudents { get; set; } = new List<NewStudentDto>();
        public string? NewTeacherEmail { get; set; }
        public string? NewTeacherName { get; set; }
        public string? NewCourseName { get; set; }

        public string TextSearch => StringHelper.GenerateTextSearch(Code, Name, Description, ScheduleDisplay);
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

