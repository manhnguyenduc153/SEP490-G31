using PRN232_be.DTO.Common;
using PRN232_be.Enums;

namespace PRN232_be.DTO.Teacher
{
    public class TeacherSearchDto : BaseSearchDto
    {
        public int? TeacherStatus { get; set; }
        public GradeLevel? GradeLevel { get; set; }
        public bool? Gender { get; set; }
    }
}
