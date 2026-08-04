using System;

namespace sep490_be.DTO.Homework
{
    // Used by students to submit their homework
    public class HomeworkSubmissionSaveDto
    {
        [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue, ErrorMessage = "ERR_HOMEWORK_ID_REQUIRED")]
        public int HomeworkId { get; set; }
        public int? StudentId { get; set; }
        public string? Content { get; set; }
        public List<string>? AttachmentUrls { get; set; }
    }

    // Used by teachers to grade a submission
    public class HomeworkSubmissionGradeDto
    {
        [System.ComponentModel.DataAnnotations.Range(0, 1000, ErrorMessage = "ERR_HOMEWORK_SCORE_INVALID")]
        public decimal Score { get; set; }
        public string? TeacherFeedback { get; set; }
    }
}

