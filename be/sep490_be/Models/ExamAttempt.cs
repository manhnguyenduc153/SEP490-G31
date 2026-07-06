using System;
using System.Collections.Generic;
using sep490_be.Models.BaseEntities;

namespace sep490_be.Models
{
    public class ExamAttempt : StandardEntity<int>
    {
        public int ExamId { get; set; }
        public int StudentId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? SubmitTime { get; set; }
        public decimal? Score { get; set; }
        public int Status { get; set; }

        public virtual Exam Exam { get; set; } = null!;
        public virtual Student Student { get; set; } = null!;
        public virtual ICollection<ExamAnswer> ExamAnswers { get; set; } = new List<ExamAnswer>();
    }
}

