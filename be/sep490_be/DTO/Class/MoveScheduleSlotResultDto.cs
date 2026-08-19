using System.Collections.Generic;

namespace sep490_be.DTO.Class
{
    public class MoveScheduleSlotResultDto
    {
        public ClassScheduleDto? UpdatedSlot { get; set; }
        public bool HasSoftConflict { get; set; }
        public List<StudentPreferenceWarningDto> Warnings { get; set; } = new List<StudentPreferenceWarningDto>();
    }

    public class StudentPreferenceWarningDto
    {
        public int StudentId { get; set; }
        public string? StudentName { get; set; }
        public string? StudentEmail { get; set; }
        public string? PreferredDays { get; set; }
        public string? PreferredSlot { get; set; }
    }
}
