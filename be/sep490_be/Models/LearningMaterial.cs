using System;
using sep490_be.Models.BaseEntities;

namespace sep490_be.Models
{
    public class LearningMaterial : StandardEntity<int>
    {
        public int? ClassId { get; set; }
        public int? ScheduleId { get; set; }
        public int? UploadedBy { get; set; }
        public int? CourseId { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? FileUrl { get; set; }
        public string? FileType { get; set; }
        public int Status { get; set; }

        public virtual Class? Class { get; set; }
        public virtual ClassSchedule? ClassSchedule { get; set; }
        public virtual Teacher? Teacher { get; set; }
        public virtual Course? Course { get; set; }
    }
}

