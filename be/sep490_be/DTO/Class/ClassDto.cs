using System;
using System.Collections.Generic;

namespace sep490_be.DTO.Class
{
    public class ClassDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Status { get; set; }
        public string? StatusName { get; set; }
        public int Type { get; set; } // 0 = Offline, 1 = Online
        public string? TypeName { get; set; } // "Offline" / "Online"
        public string? Url { get; set; }
        public string? Description { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? CourseId { get; set; }
        public string? CourseName { get; set; }
        public int? TeacherId { get; set; }
        public string? TeacherName { get; set; }
        public string? TeacherAvatar { get; set; }
        public string? ScheduleDisplay { get; set; }
        public int StudentCount { get; set; }
        public int? ExpectedLessons { get; set; }
        public string? WeeklySchedulesJson { get; set; }
        public bool AutoRefund { get; set; }
        public int? SemesterId { get; set; }
        public string? SemesterName { get; set; }
        public List<ClassScheduleDto> Schedules { get; set; } = new();
        public List<ClassStudentDto> StudentClasses { get; set; } = new();
    }

    public class ClassStudentDto
    {
        public int Id { get; set; }
        public int StudentId { get; set; }
        public int? EnrollType { get; set; } // 0 = Offline, 1 = Online
        public string? EnrollTypeName { get; set; } // "Offline" / "Online"
        public sep490_be.DTO.Student.StudentDto? Student { get; set; }
    }
}

