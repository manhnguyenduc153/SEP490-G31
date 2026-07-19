using System.Collections.Generic;
using sep490_be.DTO.StudentGrade;

namespace sep490_be.DTO.Report
{
    public class ClassGradeReportDto
    {
        public int ClassId { get; set; }
        public string ClassCode { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public List<GradeComponentDto> Components { get; set; } = new List<GradeComponentDto>();
        public List<StudentGradeRowDto> Students { get; set; } = new List<StudentGradeRowDto>();
    }

    public class StudentGradeRowDto
    {
        public int StudentId { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        
        // Key is GradeComponentId, Value is the Score
        public Dictionary<int, decimal?> ComponentScores { get; set; } = new Dictionary<int, decimal?>();
        
        public decimal? FinalScore { get; set; }
        public bool IsPassed { get; set; }
    }
}
