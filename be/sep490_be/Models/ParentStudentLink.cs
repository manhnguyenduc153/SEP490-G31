using sep490_be.Models.BaseEntities;

namespace sep490_be.Models
{
    /// <summary>
    /// Bảng trung gian liên kết Phụ huynh (ParentStudent) và Học sinh (Student).
    /// </summary>
    public class ParentStudentLink : AuditableEntity<int>
    {
        public int ParentId { get; set; }
        public int StudentId { get; set; }
        public string? Relationship { get; set; }
        public int Status { get; set; }

        public virtual ParentStudent Parent { get; set; } = null!;
        public virtual Student Student { get; set; } = null!;
    }
}
