using System;

namespace PRN232_be.DTO.Attendance
{
    public class AttendanceSaveDto
    {
        public int Id { get; set; }
        public int? ScheduleId { get; set; }
        public int? StudentId { get; set; }
        public int Status { get; set; }
        public string? Description { get; set; }
    }
}
