using PRN232_be.Models.BaseEntities;

namespace PRN232_be.Models
{
    public class StudentGradeOverride : AuditableEntity<int>
    {
        public int StudentClassId { get; set; }
        public int GradeComponentId { get; set; }
        public decimal Score { get; set; }

        public virtual StudentClass StudentClass { get; set; } = null!;
        public virtual GradeComponent GradeComponent { get; set; } = null!;
    }
}
