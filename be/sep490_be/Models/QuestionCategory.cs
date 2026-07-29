using System.Collections.Generic;
using sep490_be.Models.BaseEntities;

namespace sep490_be.Models
{
    public class QuestionCategory : StandardEntity<int>
    {
        public string? Description { get; set; }
        public int? CourseId { get; set; }
        public virtual Course? Course { get; set; }

        public virtual ICollection<Question> Questions { get; set; } = new List<Question>();
    }
}

