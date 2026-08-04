using System.Collections.Generic;
using sep490_be.DTO.Question;

namespace sep490_be.DTO.QuestionPassage
{
    public class QuestionPassageDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Content { get; set; }
        public string? AudioUrl { get; set; }
        public string? AttachmentUrl { get; set; }
        public int SkillType { get; set; }
        public string SkillTypeName { get; set; } = string.Empty;
        public int? CategoryId { get; set; }
        public string? CategoryName { get; set; }

        public List<QuestionDto> Questions { get; set; } = new List<QuestionDto>();
    }
}
