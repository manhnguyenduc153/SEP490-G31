using sep490_be.DTO;

namespace sep490_be.DTO.Student
{
    public class StudentSearchDto : BaseSearchDto
    {
        public int? StudentStatus { get; set; }
        public int? GradeLevel { get; set; }
        public bool? Gender { get; set; }
    }
}

