using System;
using System.Collections.Generic;

namespace sep490_be.DTO.Question
{
    public class QuestionDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty; // Title
        public string Content { get; set; } = string.Empty;
        public int QuestionType { get; set; }
        public string QuestionTypeName { get; set; } = string.Empty;
        public int DifficultyLevel { get; set; }
        public string DifficultyLevelName { get; set; } = string.Empty;
        public string? Explanation { get; set; }
        public int Status { get; set; }
        public int? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public decimal? Point { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }

        public List<QuestionAnswerDto> QuestionAnswers { get; set; } = new List<QuestionAnswerDto>();
    }
}

