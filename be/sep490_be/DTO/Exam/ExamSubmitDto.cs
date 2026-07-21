using System.Collections.Generic;

namespace sep490_be.DTO.Exam
{
    public class ExamSubmitDto
    {
        public int AttemptId { get; set; }
        public int TabExitsCount { get; set; }
        public string? Log { get; set; }
        public List<ExamSubmitAnswerDto> Answers { get; set; } = new List<ExamSubmitAnswerDto>();
    }

    public class ExamSubmitAnswerDto
    {
        public int QuestionId { get; set; }
        public string? AnswerContent { get; set; } // Chosen choice content or text
    }
}

