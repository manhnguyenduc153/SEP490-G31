namespace sep490_be.DTO.Attendance
{
    public class MyAttendanceClassDto
    {
        public int ClassId { get; set; }
        public string? ClassCode { get; set; }
        public string? ClassName { get; set; }
        public string? CourseName { get; set; }
        public string? TeacherName { get; set; }
        public int AttendedSessions { get; set; }
        public int AbsentSessions { get; set; }
        public int TotalSessions { get; set; }
        public double AttendanceRate { get; set; }
    }

    public class MyAttendanceSessionDto
    {
        public int ScheduleId { get; set; }
        public int LessonNo { get; set; }
        public DateTime? Date { get; set; }
        public int Status { get; set; }
        public string? StatusName { get; set; }
        public string? Description { get; set; }
    }
}
