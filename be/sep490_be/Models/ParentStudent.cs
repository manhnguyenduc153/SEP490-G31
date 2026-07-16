using System.Collections.Generic;
using sep490_be.Models.BaseEntities;

namespace sep490_be.Models
{
    /// <summary>
    /// Phụ huynh.
    /// </summary>
    public class ParentStudent : StandardEntity<int>
    {
        public string? ParentPhone { get; set; }
        public string? Email { get; set; }        
        public string? UserId { get; set; }       
        public int Status { get; set; }

        public virtual ICollection<ParentStudentLink> ParentStudentLinks { get; set; } = new List<ParentStudentLink>();
    }
}
