using PRN232_be.Models.BaseEntities;

namespace PRN232_be.Models
{
    public class TeacherAvailability : BaseEntity<int>
    {
        public int TeacherId { get; set; }
        public int SemesterId { get; set; }
        public int DayOfWeek { get; set; } // 0 = CN, 1 = T2, ..., 6 = T7
        public int SlotIndex { get; set; } // 0 đến 4 (FixedTimeSlot index)

        public virtual Teacher Teacher { get; set; } = null!;
        public virtual Semester Semester { get; set; } = null!;
    }
}
