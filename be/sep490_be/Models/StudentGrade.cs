using sep490_be.Models.BaseEntities;

namespace sep490_be.Models
{
    public class StudentGrade : BaseEntity<int>
    {
        public int StudentClassId { get; set; }
        public int ExamId { get; set; }
        public decimal? FinalScore { get; set; }
        public decimal? Weight { get; set; }
        public string? Note { get; set; }

        public virtual StudentClass StudentClass { get; set; } = null!;
        public virtual Exam Exam { get; set; } = null!;
    }
}

