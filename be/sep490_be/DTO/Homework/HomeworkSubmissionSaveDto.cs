using System;

namespace sep490_be.DTO.Homework
{
    // Used by students to submit their homework
    public class HomeworkSubmissionSaveDto
    {
        public int HomeworkId { get; set; }
        public int? StudentId { get; set; }
        public string? Content { get; set; }
        public List<string>? AttachmentUrls { get; set; }
    }

    // Used by teachers to grade a submission
    public class HomeworkSubmissionGradeDto
    {
        public decimal Score { get; set; }
        public string? TeacherFeedback { get; set; }
    }
}

