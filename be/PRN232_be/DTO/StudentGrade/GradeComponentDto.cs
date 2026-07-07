namespace PRN232_be.DTO.StudentGrade
{
    public class GradeComponentDto
    {
        public int Id { get; set; }
        public int CourseId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Weight { get; set; }
        public int SortOrder { get; set; }
        public bool IsSystem { get; set; }
    }
}
