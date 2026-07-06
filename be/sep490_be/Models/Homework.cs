using System;
using System.Collections.Generic;
using sep490_be.Models.BaseEntities;

namespace sep490_be.Models
{
    public class Homework : AuditableEntity<int>
    {
        public int ClassId { get; set; }
        public int TeacherId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public List<string>? AttachmentUrls { get; set; }
        public string? Skill { get; set; } // e.g. "Listening", "Speaking", "Reading", "Writing", "General"
        public DateTime? DueDate { get; set; }
        public decimal TotalScore { get; set; } = 10;
        public int Status { get; set; } = 1; // 1: Active, 0: Closed

        public virtual Class Class { get; set; } = null!;
        public virtual Teacher Teacher { get; set; } = null!;
        
        public virtual ICollection<HomeworkSubmission> HomeworkSubmissions { get; set; } = new List<HomeworkSubmission>();
    }
}

