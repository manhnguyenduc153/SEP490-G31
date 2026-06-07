using System.Collections.Generic;
using PRN232_be.Models.BaseEntities;

namespace PRN232_be.Models
{
    public class QuestionCategory : StandardEntity<int>
    {
        public string? Description { get; set; }

        public virtual ICollection<Question> Questions { get; set; } = new List<Question>();
    }
}
