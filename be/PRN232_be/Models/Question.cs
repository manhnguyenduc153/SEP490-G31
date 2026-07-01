using System.Collections.Generic;
using PRN232_be.Models.BaseEntities;

namespace PRN232_be.Models
{
    public class Question : StandardEntity<int>
    {
        public int? CategoryId { get; set; }
        public string Content { get; set; } = string.Empty;
        public int QuestionType { get; set; }
        public int DifficultyLevel { get; set; }
        public string? Explanation { get; set; }
        public int Status { get; set; }

        public virtual QuestionCategory? QuestionCategory { get; set; }
        public virtual ICollection<QuestionAnswer> QuestionAnswers { get; set; } = new List<QuestionAnswer>();
        public virtual ICollection<ExamQuestion> ExamQuestions { get; set; } = new List<ExamQuestion>();
        public virtual ICollection<ExamAnswer> ExamAnswers { get; set; } = new List<ExamAnswer>();
    }
}
