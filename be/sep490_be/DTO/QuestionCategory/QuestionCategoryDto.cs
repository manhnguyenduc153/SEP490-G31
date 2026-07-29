namespace sep490_be.DTO.QuestionCategory
{
    public class QuestionCategoryDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? CourseId { get; set; }
        public string? CourseName { get; set; }
        public string? CourseCode { get; set; }
    }
}

