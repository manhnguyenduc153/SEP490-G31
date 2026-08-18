using System;
using System.Collections.Generic;

namespace sep490_be.DTO.Exam
{
    public class ExamAttemptDto
    {
        public int Id { get; set; }
        public int ExamId { get; set; }
        public string ExamTitle { get; set; } = string.Empty;
        public int StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string StudentCode { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime? SubmitTime { get; set; }
        public decimal? Score { get; set; }
        public decimal? Band { get; set; } // IELTS band (Listening/Reading via correct-count lookup, Speaking/Writing via direct 0-9 grading)
        public int Status { get; set; } // 1 = In Progress, 2 = Submitted
        public int? Duration { get; set; }
        public int TabExitsCount { get; set; }
        public string? Log { get; set; }
        public string? TeacherComment { get; set; }
        public List<ExamAnswerDto> Answers { get; set; } = new List<ExamAnswerDto>();
    }

    public class GradeAttemptDto
    {
        public decimal Score { get; set; }
        public string? TeacherComment { get; set; }
    }

    public class ExamAnswerDto
    {
        public int Id { get; set; }
        public int QuestionId { get; set; }
        public string? AnswerContent { get; set; }
        public string? AttachmentUrl { get; set; }
        public decimal? Score { get; set; }
        public string? TeacherComment { get; set; }
        public bool? IsCorrect { get; set; } // Derived based on if it matches correct choices
    }
}

