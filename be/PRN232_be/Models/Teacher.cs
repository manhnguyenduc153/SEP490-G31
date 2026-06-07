using System;
using System.Collections.Generic;
using PRN232_be.Models.BaseEntities;

namespace PRN232_be.Models
{
    public class Teacher : StandardEntity<int>
    {
        public DateTime? Dob { get; set; }
        public bool? Gender { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public int Status { get; set; }
        public string? Description { get; set; }
        public int? GradeLevel { get; set; }
        public string? Avatar { get; set; }
        public string? Certificate { get; set; }

        public virtual ICollection<ClassSchedule> ClassSchedules { get; set; } = new List<ClassSchedule>();
        public virtual ICollection<LearningMaterial> LearningMaterials { get; set; } = new List<LearningMaterial>();
        public virtual ICollection<ActivityAnswer> ActivityAnswers { get; set; } = new List<ActivityAnswer>();
        public virtual ICollection<ExamSchedule> ExamSchedules { get; set; } = new List<ExamSchedule>();
    }
}
