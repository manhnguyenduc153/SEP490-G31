namespace sep490_be.DTO.StudentGrade
{
    public class MyGradeExamDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public decimal TotalScore { get; set; }
        public decimal? Score { get; set; }
        public decimal? NormalizedScore { get; set; }
        public decimal? Band { get; set; }
    }
}
