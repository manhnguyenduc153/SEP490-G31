using System.Collections.Generic;

namespace sep490_be.DTO.Question
{
    public class QuestionSaveDto
    {
        public int Id { get; set; }
        public string? Code { get; set; }
        public string Name { get; set; } = string.Empty; // Title
        public string Content { get; set; } = string.Empty;
        public string? Instruction { get; set; }
        public int QuestionType { get; set; }
        public int SkillType { get; set; }
        public int DifficultyLevel { get; set; }
        public string? Explanation { get; set; }
        public string? MediaUrl { get; set; }
        public int? CategoryId { get; set; }
        public int? PassageId { get; set; }
        public decimal? Point { get; set; }

        public List<QuestionAnswerDto> QuestionAnswers { get; set; } = new List<QuestionAnswerDto>();
        public List<QuestionAnswerDto> Answers { get => QuestionAnswers; set => QuestionAnswers = value; }
    }
}

