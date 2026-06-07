using System;
using System.Collections.Generic;
using PRN232_be.Models.BaseEntities;

namespace PRN232_be.Models
{
    public class Student : StandardEntity<int>
    {
        public DateTime? Dob { get; set; }
        public bool? Gender { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public int Status { get; set; }
        public string? Description { get; set; }
        public string? SchoolName { get; set; }
        public int? GradeLevel { get; set; }
        public string? ParentName { get; set; }
        public string? ParentPhone { get; set; }
        public string? Avatar { get; set; }

        public virtual ICollection<ParentStudent> ParentStudents { get; set; } = new List<ParentStudent>();
        public virtual ICollection<StudentClass> StudentClasses { get; set; } = new List<StudentClass>();
        public virtual ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
        public virtual ICollection<ActivityAttempt> ActivityAttempts { get; set; } = new List<ActivityAttempt>();
        public virtual ICollection<ExamStudent> ExamStudents { get; set; } = new List<ExamStudent>();
    }
}
