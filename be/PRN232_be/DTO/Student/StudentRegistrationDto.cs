using System.Collections.Generic;

namespace PRN232_be.DTO.Student
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
        public int Status { get; set; }
        public string? StatusName { get; set; }
    }
}
