using System.Collections.Generic;
using sep490_be.Enums;
using sep490_be.Models.BaseEntities;

namespace sep490_be.Models
{
    public class Room : StandardEntity<int>
    {
        public int? Capacity { get; set; }
        public int Status { get; set; }

        public string? Building { get; set; }
        public string? Floor { get; set; }

        public virtual ICollection<ClassSchedule> ClassSchedules { get; set; } = new List<ClassSchedule>();
        public virtual ICollection<ExamSchedule> ExamSchedules { get; set; } = new List<ExamSchedule>();
    }
}

