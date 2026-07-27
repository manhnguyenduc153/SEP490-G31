using System.Collections.Generic;

namespace sep490_be.DTO.Student
{
    public class StudentRegistrationSaveDto
    {
        public int Id { get; set; }
        
        // These fields can be used for Excel import auto-creation or lookup
        public string? StudentCode { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string StudentEmail { get; set; } = string.Empty;
        public string? StudentPhone { get; set; }

        /// <summary>
        /// CourseId = 0 means "auto-resolve by CourseName". System will find or create the course.
        /// </summary>
        public int CourseId { get; set; }

        /// <summary>
        /// Used when CourseId is 0. System will find existing course by name or auto-create one.
        /// </summary>
        public string? CourseName { get; set; }

        public int SemesterId { get; set; }
        public List<string> PreferredSlots { get; set; } = new List<string>();
        public int Status { get; set; }
        public int EnrollType { get; set; } = 0; // 0 = Offline, 1 = Online
    }
}

