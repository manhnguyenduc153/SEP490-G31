using System;
using sep490_be.Models.BaseEntities;

namespace sep490_be.Models
{
    public class Notification : StandardEntity<int>
    {
        public string? Title { get; set; }
        public string? Content { get; set; }
        public int Status { get; set; }
        public int? ClassId { get; set; }
        public int? TargetType { get; set; }
        public int? TargetId { get; set; }
        public int? SentBy { get; set; }
        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        public virtual Class? Class { get; set; }
    }
}

