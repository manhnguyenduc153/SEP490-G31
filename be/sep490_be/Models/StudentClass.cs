using System;
using System.Collections.Generic;
using sep490_be.Models.BaseEntities;

namespace sep490_be.Models
{
    public class StudentClass : BaseEntity<int>
    {
        public int StudentId { get; set; }
        public int ClassId { get; set; }
        public DateTime? EnrollDate { get; set; }
        public DateTime? CompletionDate { get; set; }
        public int Status { get; set; }
        public string? Description { get; set; }

        public virtual Student Student { get; set; } = null!;
        public virtual Class Class { get; set; } = null!;
        public virtual ICollection<StudentGrade> StudentGrades { get; set; } = new List<StudentGrade>();
        public virtual ICollection<StudentGradeOverride> StudentGradeOverrides { get; set; } = new List<StudentGradeOverride>();
    }
}

