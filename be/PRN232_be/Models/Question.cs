using System.Collections.Generic;
using PRN232_be.Models.BaseEntities;

namespace PRN232_be.Models
{
    public class Question : BaseEntity<int>
    {
        public int? CategoryId { get; set; }
        public string Content { get; set; } = string.Empty;
        public int QuestionType { get; set; }
        public int DifficultyLevel { get; set; }
        public string? Explanation { get; set; }
        public int Status { get; set; }

        public virtual QuestionCategory? QuestionCategory { get; set; }
        public virtual ICollection<QuestionAnswer> QuestionAnswers { get; set; } = new List<QuestionAnswer>();
        public virtual ICollection<ActivityQuestion> ActivityQuestions { get; set; } = new List<ActivityQuestion>();
        public virtual ICollection<ActivityAnswer> ActivityAnswers { get; set; } = new List<ActivityAnswer>();
    }
}
