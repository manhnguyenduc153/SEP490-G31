using System;
using System.ComponentModel.DataAnnotations;

namespace sep490_be.DTO.Homework
{
    public class HomeworkSaveDto
    {
        public int Id { get; set; }
        
        [Range(1, int.MaxValue, ErrorMessage = "ERR_HOMEWORK_CLASS_REQUIRED")]
        public int ClassId { get; set; }
        
        [Range(1, int.MaxValue, ErrorMessage = "ERR_HOMEWORK_TEACHER_REQUIRED")]
        public int TeacherId { get; set; }
        
        [Required(ErrorMessage = "ERR_HOMEWORK_TITLE_REQUIRED")]
        [StringLength(500, ErrorMessage = "ERR_HOMEWORK_TITLE_MAX_LENGTH")]
        public string Title { get; set; } = string.Empty;
        
        public string? Description { get; set; }
        
        public List<string>? AttachmentUrls { get; set; }
        
        public string? Skill { get; set; }
        
        public DateTime? DueDate { get; set; }
        
        [Range(0, 1000, ErrorMessage = "ERR_HOMEWORK_TOTAL_SCORE_INVALID")]
        public decimal TotalScore { get; set; } = 10;
        
        [Range(0, 1, ErrorMessage = "ERR_HOMEWORK_STATUS_INVALID")]
        public int Status { get; set; } = 1;
    }
}

