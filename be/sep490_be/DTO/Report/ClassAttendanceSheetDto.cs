using System.Collections.Generic;

namespace sep490_be.DTO.Report
{
    public class ClassAttendanceSheetDto
    {
        public int ClassId { get; set; }
        public string? ClassCode { get; set; }
        public string? ClassName { get; set; }
        public int TotalSessions { get; set; }
        public int CompletedSessions { get; set; }
        public double AverageAttendanceRate { get; set; }
        
        public List<ClassAttendanceHeaderDto> Sessions { get; set; } = new();
        public List<ClassAttendanceStudentRowDto> Students { get; set; } = new();
    }

    public class ClassAttendanceHeaderDto
    {
        public int ScheduleId { get; set; }
        public int LessonNo { get; set; }
        public string? Date { get; set; }
    }

    public class ClassAttendanceStudentRowDto
    {
        public int StudentId { get; set; }
        public string? StudentCode { get; set; }
        public string? StudentName { get; set; }
        public int PresentCount { get; set; }
        public int AbsentCount { get; set; }
        public double AttendanceRate { get; set; }
        public List<ClassAttendanceStatusDto> Attendances { get; set; } = new();
    }

    public class ClassAttendanceStatusDto
    {
        public int ScheduleId { get; set; }
        public int Status { get; set; } // 1: Present, 0: Absent, 2: Late, 3: Excused, -1: Not taken
        public string? Description { get; set; }
    }
}
