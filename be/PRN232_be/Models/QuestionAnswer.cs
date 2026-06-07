using PRN232_be.Models.BaseEntities;

namespace PRN232_be.Models
{
    public class QuestionAnswer : StandardEntity<int>
    {
        public int? QuestionId { get; set; }
        public string Content { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }

        public virtual Question? Question { get; set; }
    }
}
