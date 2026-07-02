using System;
using System.Collections.Generic;

namespace PRN232_be.DTO.Class
{
    public class ClassDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Status { get; set; }
        public string? StatusName { get; set; }
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
        public List<ClassScheduleDto> Schedules { get; set; } = new();
        public List<ClassStudentDto> StudentClasses { get; set; } = new();
    }

    public class ClassStudentDto
    {
        public int Id { get; set; }
        public int StudentId { get; set; }
        public PRN232_be.DTO.Student.StudentDto? Student { get; set; }
    }
}
