namespace PRN232_be.DTO.StudentGrade
{
    public class GradeComponentSaveDto
    {
        public int? Id { get; set; }
        public string? Code { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Weight { get; set; }
        public int SortOrder { get; set; }
        public bool IsSystem { get; set; }
    }
}
