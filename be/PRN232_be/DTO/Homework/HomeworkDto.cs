using System;

namespace PRN232_be.DTO.Homework
{
    public class HomeworkDto
    {
        public int Id { get; set; }
        public int ClassId { get; set; }
        public int TeacherId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public List<string>? AttachmentUrls { get; set; }
        public string? Skill { get; set; }
        public DateTime? DueDate { get; set; }
        public decimal TotalScore { get; set; }
        public int Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public string? TeacherName { get; set; }
        public string? ClassName { get; set; }
    }
}
