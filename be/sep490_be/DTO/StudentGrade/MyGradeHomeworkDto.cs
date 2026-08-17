namespace sep490_be.DTO.StudentGrade
{
    public class MyGradeHomeworkDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public decimal TotalScore { get; set; }
        public decimal? Score { get; set; }
        public decimal? NormalizedScore { get; set; }
    }
}
