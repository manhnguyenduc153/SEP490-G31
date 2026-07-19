using System;
using System.Collections.Generic;

namespace sep490_be.DTO.Report
{
    public class ExamResultReportDto
    {
        public int ExamId { get; set; }
        public string ExamTitle { get; set; } = string.Empty;
        public decimal TotalScore { get; set; }
        public decimal PassingScore { get; set; }
        public int TotalStudents { get; set; }
        public int ParticipatedStudents { get; set; }
        public int PassedStudents { get; set; }
        public int FailedStudents { get; set; }
        public decimal AverageScore { get; set; }
        public decimal PassRate { get; set; }

        public List<StudentExamResultDto> StudentResults { get; set; } = new List<StudentExamResultDto>();
    }

    public class StudentExamResultDto
    {
        public int StudentId { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public int AttemptCount { get; set; }
        public decimal? FinalScore { get; set; }
        public bool IsPassed { get; set; }
        public DateTime? SubmittedAt { get; set; }
    }
}
