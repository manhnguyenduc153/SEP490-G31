using PRN232_be.Models.BaseEntities;

namespace PRN232_be.Models
{
    /// <summary>
    /// Phụ huynh của học sinh.
    /// </summary>
    public class ParentStudent : StandardEntity<int>
    {
        public int StudentId { get; set; }
        public string? ParentPhone { get; set; }
        public string? Email { get; set; }        
        public string? UserId { get; set; }       
        public string? Relationship { get; set; }
        public int Status { get; set; }

        public virtual Student Student { get; set; } = null!;
    }
}
