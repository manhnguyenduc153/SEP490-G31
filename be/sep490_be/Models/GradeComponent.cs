using PRN232_be.Models.BaseEntities;

namespace PRN232_be.Models
{
    public class GradeComponent : AuditableEntity<int>
    {
        public int CourseId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Weight { get; set; }
        public int SortOrder { get; set; }
        public bool IsSystem { get; set; }

        public virtual Course Course { get; set; } = null!;
        public virtual ICollection<StudentGradeOverride> StudentGradeOverrides { get; set; } = new List<StudentGradeOverride>();
    }
}
