using PRN232_be.Models.BaseEntities;

namespace PRN232_be.Models
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
