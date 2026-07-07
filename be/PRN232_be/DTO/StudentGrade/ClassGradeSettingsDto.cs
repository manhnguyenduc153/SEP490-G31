namespace PRN232_be.DTO.StudentGrade
{
    public class ClassGradeSettingsDto
    {
        public int ClassId { get; set; }
        public int CourseId { get; set; }
        public List<GradeComponentDto> Components { get; set; } = new();
        public List<StudentGradeOverrideDto> Overrides { get; set; } = new();
    }
}
