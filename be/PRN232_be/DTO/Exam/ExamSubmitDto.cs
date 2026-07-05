using System.Collections.Generic;

namespace PRN232_be.DTO.Exam
{
    public class ExamSubmitDto
    {
        public int AttemptId { get; set; }
        public List<ExamSubmitAnswerDto> Answers { get; set; } = new List<ExamSubmitAnswerDto>();
    }

    public class ExamSubmitAnswerDto
    {
        public int QuestionId { get; set; }
        public string? AnswerContent { get; set; } // Chosen choice content or text
    }
}
