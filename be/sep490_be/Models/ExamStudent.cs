using sep490_be.Models.BaseEntities;

namespace sep490_be.Models
{
    public class ExamStudent : BaseEntity<int>
    {
        public int ExamScheduleId { get; set; }
        public int StudentId { get; set; }
        public int Status { get; set; }

        public virtual ExamSchedule ExamSchedule { get; set; } = null!;
        public virtual Student Student { get; set; } = null!;
    }
}

