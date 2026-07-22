using sep490_be.DTO.Common;

namespace sep490_be.DTO.Teacher
{
    public class TeacherSearchDto : BaseSearchDto
    {
        public int? TeacherStatus { get; set; }
        public bool? Gender { get; set; }
    }
}

