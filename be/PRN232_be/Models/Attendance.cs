using System;
using PRN232_be.Models.BaseEntities;

namespace PRN232_be.Models
{
    public class Attendance : BaseEntity<int>
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
