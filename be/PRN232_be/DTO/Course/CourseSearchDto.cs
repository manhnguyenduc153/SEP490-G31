namespace PRN232_be.DTO.Course
{
    public class CourseSearchDto
    {
        public string? Keyword { get; set; }
        public int? Status { get; set; }
        public int PageIndex { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
