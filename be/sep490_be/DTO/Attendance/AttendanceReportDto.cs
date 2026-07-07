using System.Collections.Generic;

namespace sep490_be.DTO.Attendance
{
    public class AttendanceReportDto
    {
        public List<AttendanceReportHeaderDto> Sessions { get; set; } = new();
        public List<AttendanceReportStudentRowDto> Students { get; set; } = new();
    }

    public class AttendanceReportHeaderDto
    {
        public int ScheduleId { get; set; }
        public int LessonNo { get; set; }
        public string? Date { get; set; }
    }

    public class AttendanceReportStudentRowDto
    {
        public int StudentId { get; set; }
        public string? StudentCode { get; set; }
        public string? StudentName { get; set; }
        public List<AttendanceReportStatusDto> Attendances { get; set; } = new();
    }

    public class AttendanceReportStatusDto
    {
        public int ScheduleId { get; set; }
        public int Status { get; set; } // 1: Present, 0: Absent, 2: Late, 3: Excused, -1: Not taken
        public string? Description { get; set; }
    }
}

