using PRN232_be.Models.BaseEntities;

namespace PRN232_be.Models
{
    public class ParentStudent : BaseEntity<int>
    {
        public int StudentId { get; set; }
        public string? ParentName { get; set; }
        public string? ParentPhone { get; set; }
        public string? Relationship { get; set; }
        public int Status { get; set; }

        public virtual Student Student { get; set; } = null!;
    }
}
