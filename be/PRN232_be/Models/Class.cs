using System;
using System.Collections.Generic;
using PRN232_be.Models.BaseEntities;

namespace PRN232_be.Models
{
    public class Class : StandardEntity<int>
    {
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

        public virtual Course? Course { get; set; }
        public virtual Teacher? Teacher { get; set; }
        public virtual ICollection<StudentClass> StudentClasses { get; set; } = new List<StudentClass>();
        public virtual ICollection<ClassSchedule> ClassSchedules { get; set; } = new List<ClassSchedule>();
        public virtual ICollection<LearningMaterial> LearningMaterials { get; set; } = new List<LearningMaterial>();
        public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
        public virtual ICollection<Exam> Exams { get; set; } = new List<Exam>();
    }
}
