namespace sep490_be.DTO.Teacher
{
    public class TeacherAvailabilityDto
    {
        public int Id { get; set; }
        public int TeacherId { get; set; }
        public string? TeacherName { get; set; }
        public string? TeacherCode { get; set; }
        public int SemesterId { get; set; }
        public string? SemesterName { get; set; }
        public int DayOfWeek { get; set; }
        public int SlotIndex { get; set; }
        public string? SlotName { get; set; } // e.g. "Ca 1 (07:30 - 09:30)"
    }
}

