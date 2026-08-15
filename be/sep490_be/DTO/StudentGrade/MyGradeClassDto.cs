namespace sep490_be.DTO.StudentGrade
{
    public class MyGradeClassDto
    {
        public int ClassId { get; set; }
        public string? ClassCode { get; set; }
        public string? ClassName { get; set; }
        public int? CourseId { get; set; }
        public string? CourseCode { get; set; }
        public string? CourseName { get; set; }
        public decimal AverageScore { get; set; }
        public List<MyGradeComponentScoreDto> Components { get; set; } = new();
        public List<MyGradeHomeworkDto> Homeworks { get; set; } = new();
        public List<MyGradeExamDto> Exams { get; set; } = new();
    }
}
