using sep490_be.Models.BaseEntities;

namespace sep490_be.Models
{
    public class ExamQuestion : BaseEntity<int>
    {
        public int ExamId { get; set; }
        public int QuestionId { get; set; }
        public decimal Point { get; set; }

        public virtual Exam Exam { get; set; } = null!;
        public virtual Question Question { get; set; } = null!;
    }
}

