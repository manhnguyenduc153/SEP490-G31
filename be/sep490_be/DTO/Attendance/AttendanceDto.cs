using System;

namespace sep490_be.DTO.Attendance
{
    public class AttendanceDto
    {
        public int Id { get; set; }
        public int? ScheduleId { get; set; }
        public int? StudentId { get; set; }
        public string? StudentCode { get; set; }
        public string? StudentName { get; set; }
        public int Status { get; set; }
        public string? StatusName { get; set; }
        public DateTime? CheckInTime { get; set; }
        public string? Description { get; set; }
    }
}

