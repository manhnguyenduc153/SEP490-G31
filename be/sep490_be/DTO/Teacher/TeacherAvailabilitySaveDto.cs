using System.Collections.Generic;

namespace sep490_be.DTO.Teacher
{
    public class TeacherAvailabilitySaveDto
    {
        public int TeacherId { get; set; }
        public int SemesterId { get; set; }
        public List<TeacherAvailabilitySlotDto> Slots { get; set; } = new List<TeacherAvailabilitySlotDto>();
    }

    public class TeacherAvailabilitySlotDto
    {
        public int DayOfWeek { get; set; }
        public int SlotIndex { get; set; }
    }
}

