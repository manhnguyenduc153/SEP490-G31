using System.Collections.Generic;

namespace sep490_be.DTO.Attendance
{
    public class AttendanceBulkSaveDto
    {
        public int ScheduleId { get; set; }
        public List<AttendanceStudentSaveDto> Attendances { get; set; } = new();
    }

    public class AttendanceStudentSaveDto
    {
        public int StudentId { get; set; }
        public int Status { get; set; }
        public string? Description { get; set; }
    }
}

