using System.Collections.Generic;
using sep490_be.Models.BaseEntities;

namespace sep490_be.Models
{
    public class QuestionPassage : StandardEntity<int>
    {
        public string Title { get; set; } = string.Empty;
        public string? Content { get; set; }
        public string? AudioUrl { get; set; }
        public string? AttachmentUrl { get; set; }
        public int SkillType { get; set; } = 1;
        public int? CategoryId { get; set; }

        public virtual QuestionCategory? QuestionCategory { get; set; }
        public virtual ICollection<Question> Questions { get; set; } = new List<Question>();
    }
}
