using System;
using PRN232_be.Models.BaseEntities;

namespace PRN232_be.Models
{
    public class Notification : BaseEntity<int>
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
