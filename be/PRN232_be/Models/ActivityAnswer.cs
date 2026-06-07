using System;
using PRN232_be.Models.BaseEntities;

namespace PRN232_be.Models
{
    public class ActivityAnswer : StandardEntity<int>
    {
        public int AttemptId { get; set; }
        public int QuestionId { get; set; }
        public string? AnswerContent { get; set; }
        public string? AttachmentUrl { get; set; }
        public decimal? Score { get; set; }
        public string? TeacherComment { get; set; }
        public int? GradedBy { get; set; }
        public DateTime? GradedAt { get; set; }

        public virtual ActivityAttempt ActivityAttempt { get; set; } = null!;
        public virtual Question Question { get; set; } = null!;
        public virtual Teacher? Teacher { get; set; }
    }
}
