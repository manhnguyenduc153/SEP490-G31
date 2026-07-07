namespace PRN232_be.DTO.StudentGrade
{
    public class StudentGradeOverrideDto
    {
        public int Id { get; set; }
        public int StudentClassId { get; set; }
        public int StudentId { get; set; }
        public int GradeComponentId { get; set; }
        public string ComponentCode { get; set; } = string.Empty;
        public decimal Score { get; set; }
    }
}
