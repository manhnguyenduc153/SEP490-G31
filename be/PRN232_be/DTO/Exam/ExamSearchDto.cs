namespace PRN232_be.DTO.Exam
{
    public class ExamSearchDto
    {
        public string? Keyword { get; set; }
        public int? ClassId { get; set; }
        public int? Status { get; set; }
        public int? Type { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
