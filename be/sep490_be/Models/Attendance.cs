using System;
using sep490_be.Models.BaseEntities;

namespace sep490_be.Models
{
    public class Attendance : StandardEntity<int>
    {
        public int? ScheduleId { get; set; }
        public int? StudentId { get; set; }
        public int Status { get; set; }
        public DateTime? CheckInTime { get; set; }
        public string? Description { get; set; }

        public virtual ClassSchedule? ClassSchedule { get; set; }
        public virtual Student? Student { get; set; }
    }
}

