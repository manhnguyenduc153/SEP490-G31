using sep490_be.DTO.Common;
using sep490_be.Enums;

namespace sep490_be.DTO.Teacher
{
    public class TeacherSearchDto : BaseSearchDto
    {
        public int? TeacherStatus { get; set; }
        public GradeLevel? GradeLevel { get; set; }
        public bool? Gender { get; set; }
    }
}

