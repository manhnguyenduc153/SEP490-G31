using System;

namespace sep490_be.DTO.LearningMaterial
{
    public class LearningMaterialDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int? ClassId { get; set; }
        public string? ClassName { get; set; }
        public int? ScheduleId { get; set; }
        public string? ScheduleName { get; set; }
        public int? UploadedBy { get; set; }
        public string? TeacherName { get; set; }
        public int? CourseId { get; set; }
        public string? CourseName { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? FileUrl { get; set; }
        public string? FileType { get; set; }
        public int Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
    }
}

