using System;
using System.Collections.Generic;
using sep490_be.Models.BaseEntities;

namespace sep490_be.Models
{
    public class ExamSchedule : StandardEntity<int>
    {
        public int? ExamId { get; set; }
        public int? RoomId { get; set; }
        public DateTime? ExamDate { get; set; }
        public int? SlotId { get; set; }
        public int? SupervisorId { get; set; }
        public int Status { get; set; }
        public string? Note { get; set; }

        public virtual Exam? Exam { get; set; }
        public virtual Room? Room { get; set; }
        public virtual TimeSlot? TimeSlot { get; set; }
        public virtual Teacher? Supervisor { get; set; }

        public virtual ICollection<ExamStudent> ExamStudents { get; set; } = new List<ExamStudent>();
    }
}

