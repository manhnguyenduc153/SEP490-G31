using System;
using System.Collections.Generic;
using PRN232_be.Models.BaseEntities;

namespace PRN232_be.Models
{
    public class ClassSchedule : StandardEntity<int>
    {
        public int? ClassId { get; set; }
        public int? LessonNo { get; set; }
        public DateTime? ScheduleDate { get; set; }
        public int? SlotId { get; set; }
        public int? RoomId { get; set; }
        public int? TeacherId { get; set; }
        public int Status { get; set; }
        public string? Note { get; set; }

        public virtual Class? Class { get; set; }
        public virtual TimeSlot? TimeSlot { get; set; }
        public virtual Room? Room { get; set; }
        public virtual Teacher? Teacher { get; set; }

        public virtual ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
        public virtual ICollection<LearningMaterial> LearningMaterials { get; set; } = new List<LearningMaterial>();
        public virtual ICollection<Activity> Activities { get; set; } = new List<Activity>();
    }
}
