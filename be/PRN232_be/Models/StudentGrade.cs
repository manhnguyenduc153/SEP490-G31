using PRN232_be.Models.BaseEntities;

namespace PRN232_be.Models
{
    public class StudentGrade : BaseEntity<int>
    {
        public int StudentClassId { get; set; }
        public int ActivityId { get; set; }
        public decimal? FinalScore { get; set; }
        public decimal? Weight { get; set; }
        public string? Note { get; set; }

        public virtual StudentClass StudentClass { get; set; } = null!;
        public virtual Activity Activity { get; set; } = null!;
    }
}
