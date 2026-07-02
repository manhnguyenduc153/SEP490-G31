using System;
using System.ComponentModel.DataAnnotations;

namespace PRN232_be.DTO.Homework
{
    public class HomeworkSaveDto
    {
        public int Id { get; set; }
        
        [Required(ErrorMessage = "ClassId is required")]
        public int ClassId { get; set; }
        
        [Required(ErrorMessage = "TeacherId is required")]
        public int TeacherId { get; set; }
        
        [Required(ErrorMessage = "Title is required")]
        [StringLength(500, ErrorMessage = "Title cannot exceed 500 characters")]
        public string Title { get; set; } = string.Empty;
        
        public string? Description { get; set; }
        
        public List<string>? AttachmentUrls { get; set; }
        
        public string? Skill { get; set; }
        
        public DateTime? DueDate { get; set; }
        
        [Range(0, 1000, ErrorMessage = "TotalScore must be between 0 and 1000")]
        public decimal TotalScore { get; set; } = 10;
        
        public int Status { get; set; } = 1;
    }
}
