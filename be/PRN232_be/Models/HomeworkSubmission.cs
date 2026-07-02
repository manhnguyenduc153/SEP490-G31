using System;
using System.Collections.Generic;
using PRN232_be.Models.BaseEntities;

namespace PRN232_be.Models
{
    public class HomeworkSubmission : AuditableEntity<int>
    {
        public int HomeworkId { get; set; }
        public int StudentId { get; set; }
        public string? Content { get; set; }
        public List<string>? AttachmentUrls { get; set; }
        public DateTime SubmitTime { get; set; }
        public decimal? Score { get; set; }
        public string? TeacherFeedback { get; set; }
        public int Status { get; set; } = 1; // 1: Submitted, 2: Graded, 3: Late

        public virtual Homework Homework { get; set; } = null!;
        public virtual Student Student { get; set; } = null!;
    }
}
