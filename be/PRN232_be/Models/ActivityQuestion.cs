using PRN232_be.Models.BaseEntities;

namespace PRN232_be.Models
{
    public class ActivityQuestion : BaseEntity<int>
    {
        public int ActivityId { get; set; }
        public int QuestionId { get; set; }
        public decimal Point { get; set; }

        public virtual Activity Activity { get; set; } = null!;
        public virtual Question Question { get; set; } = null!;
    }
}
