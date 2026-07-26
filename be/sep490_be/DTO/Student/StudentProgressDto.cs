using System.Collections.Generic;
using sep490_be.DTO.Homework;
using sep490_be.DTO.Attendance;

namespace sep490_be.DTO.Student
{
    public class StudentProgressDto
    {
        public List<HomeworkWithSubmissionDto> Homeworks { get; set; } = new();
        public List<MyAttendanceSessionDto> AttendanceSessions { get; set; } = new();
    }

    public class HomeworkWithSubmissionDto : HomeworkDto
    {
        public HomeworkSubmissionDto? Submission { get; set; }
    }
}
