using sep490_be.Models.BaseEntities;

namespace sep490_be.Models
{
    public class QuestionAnswer : StandardEntity<int>
    {
        public int? QuestionId { get; set; }
        public string Content { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }

        public virtual Question? Question { get; set; }
    }
}

