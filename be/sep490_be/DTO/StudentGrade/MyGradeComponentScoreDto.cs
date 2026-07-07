namespace sep490_be.DTO.StudentGrade
{
    public class MyGradeComponentScoreDto
    {
        public int GradeComponentId { get; set; }
        public string ComponentCode { get; set; } = string.Empty;
        public string ComponentName { get; set; } = string.Empty;
        public decimal Weight { get; set; }
        public decimal Score { get; set; }
        public decimal RawScore { get; set; }
        public bool IsOverride { get; set; }
    }
}
