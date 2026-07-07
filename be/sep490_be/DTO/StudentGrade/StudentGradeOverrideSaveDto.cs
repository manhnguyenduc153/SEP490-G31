namespace sep490_be.DTO.StudentGrade
{
    public class StudentGradeOverrideSaveDto
    {
        public int StudentClassId { get; set; }
        public int GradeComponentId { get; set; }
        public decimal? Score { get; set; }
    }
}
