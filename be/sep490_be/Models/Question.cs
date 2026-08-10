using System.Collections.Generic;
using sep490_be.Models.BaseEntities;

namespace sep490_be.Models
{
    public class Question : StandardEntity<int>
    {
        public int? CategoryId { get; set; }
        public int? PassageId { get; set; }
        public string Content { get; set; } = string.Empty;
        // Shared instruction shown once above a group of questions of the same sub-format
        // (e.g. "Which paragraph contains each of the following pieces of information?").
        // Only the first question of a group needs it set; later questions in the same
        // group leave it null.
        public string? Instruction { get; set; }
        public int QuestionType { get; set; }
        public int SkillType { get; set; } = 1;
        public int DifficultyLevel { get; set; }
        public string? Explanation { get; set; }
        public string? MediaUrl { get; set; }
        public int Status { get; set; }
        public decimal? Point { get; set; }

        public virtual QuestionCategory? QuestionCategory { get; set; }
        public virtual QuestionPassage? QuestionPassage { get; set; }
        public virtual ICollection<QuestionAnswer> QuestionAnswers { get; set; } = new List<QuestionAnswer>();
        public virtual ICollection<ExamQuestion> ExamQuestions { get; set; } = new List<ExamQuestion>();
        public virtual ICollection<ExamAnswer> ExamAnswers { get; set; } = new List<ExamAnswer>();
    }
}

