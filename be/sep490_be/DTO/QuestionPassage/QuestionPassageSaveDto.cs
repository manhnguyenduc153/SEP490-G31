using System.Collections.Generic;
using sep490_be.DTO.Question;
using sep490_be.Helpers;

namespace sep490_be.DTO.QuestionPassage
{
    public class QuestionPassageSaveDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Content { get; set; }
        public string? AudioUrl { get; set; }
        public string? AttachmentUrl { get; set; }
        public int SkillType { get; set; } = 1;
        public int? CategoryId { get; set; }

        public List<QuestionSaveDto> Questions { get; set; } = new List<QuestionSaveDto>();

        public string TextSearch => StringHelper.GenerateTextSearch(Code, Title, Content);
    }
}
