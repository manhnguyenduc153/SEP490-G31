using System;

namespace sep490_be.DTO.Homework
{
    public class HomeworkSubmissionDto
    {
        public int Id { get; set; }
        public int HomeworkId { get; set; }
        public int StudentId { get; set; }
        public string? Content { get; set; }
        public List<string>? AttachmentUrls { get; set; }
        public DateTime SubmitTime { get; set; }
        public decimal? Score { get; set; }
        public string? TeacherFeedback { get; set; }
        public int Status { get; set; }
        
        public string? StudentName { get; set; }
        public string? StudentCode { get; set; }
        public string? StudentEmail { get; set; }
    }
}

