using System.Collections.Generic;

namespace sep490_be.DTO.Student
{
    public class StudentRegistrationDto
    {
        public int Id { get; set; }
        public int StudentId { get; set; }
        public string? StudentCode { get; set; }
        public string? StudentName { get; set; }
        public string? StudentEmail { get; set; }
        public string? StudentPhone { get; set; }
        public int CourseId { get; set; }
        public string? CourseName { get; set; }
        public int SemesterId { get; set; }
        public string? SemesterName { get; set; }
        public List<string> PreferredSlots { get; set; } = new List<string>();
        public int? PreferredSlotIndex { get; set; }
        public int? PreferredDaysOfWeek { get; set; }
        public int Status { get; set; }
        public string? StatusName { get; set; }
        public int EnrollType { get; set; } // 0 = Offline, 1 = Online
        public string? EnrollTypeName { get; set; } // "Offline" / "Online"
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}

