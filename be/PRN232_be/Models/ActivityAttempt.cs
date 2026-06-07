using System;
using System.Collections.Generic;
using PRN232_be.Models.BaseEntities;

namespace PRN232_be.Models
{
    public class ActivityAttempt : BaseEntity<int>
    {
        public int ActivityId { get; set; }
        public int StudentId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? SubmitTime { get; set; }
        public decimal? Score { get; set; }
        public int Status { get; set; }

        public virtual Activity Activity { get; set; } = null!;
        public virtual Student Student { get; set; } = null!;
        public virtual ICollection<ActivityAnswer> ActivityAnswers { get; set; } = new List<ActivityAnswer>();
    }
}
