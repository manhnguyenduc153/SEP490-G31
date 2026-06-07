using System;
using PRN232_be.Models.BaseEntities;

namespace PRN232_be.Models
{
    public class LearningMaterial : BaseEntity<int>
    {
        public int? ClassId { get; set; }
        public int? ScheduleId { get; set; }
        public int? UploadedBy { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? FileUrl { get; set; }
        public string? FileType { get; set; }
        public int Status { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual Class? Class { get; set; }
        public virtual ClassSchedule? ClassSchedule { get; set; }
        public virtual Teacher? Teacher { get; set; }
    }
}
