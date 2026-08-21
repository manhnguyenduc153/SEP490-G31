using sep490_be.Models.BaseEntities;

namespace sep490_be.Models
{
    public class StudentRegistration : AuditableEntity<int>
    {
        public int StudentId { get; set; }
        public int CourseId { get; set; }
        public int SemesterId { get; set; }
        public string PreferredSlotsJson { get; set; } = "[]"; // e.g., ["morning", "evening"] (deprecated, kept for backward compatibility)
        public int? PreferredSlotIndex { get; set; } // 0-4 corresponding to FixedTimeSlot indices
        public int? PreferredDaysOfWeek { get; set; } // Bitmask: CN=1, T2=2, T3=4, T4=8, T5=16, T6=32, T7=64
        public int Status { get; set; } // 0: Pending, 1: Scheduled, 2: Cancelled
        public int EnrollType { get; set; } = 0; // 0 = Offline, 1 = Online

        public virtual Student Student { get; set; } = null!;
        public virtual Course Course { get; set; } = null!;
        public virtual Semester Semester { get; set; } = null!;
    }
}

