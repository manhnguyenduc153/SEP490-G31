using PRN232_be.Models.BaseEntities;

namespace PRN232_be.Models
{
    public class StudentRegistration : BaseEntity<int>
    {
        public int StudentId { get; set; }
        public int CourseId { get; set; }
        public int SemesterId { get; set; }
        public string PreferredSlotsJson { get; set; } = "[]"; // e.g., ["morning", "evening"]
        public int Status { get; set; } // 0: Pending, 1: Scheduled, 2: Cancelled

        public virtual Student Student { get; set; } = null!;
        public virtual Course Course { get; set; } = null!;
        public virtual Semester Semester { get; set; } = null!;
    }
}
