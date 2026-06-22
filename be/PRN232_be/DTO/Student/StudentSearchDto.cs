using PRN232_be.DTO;

namespace PRN232_be.DTO.Student
{
    public class StudentSearchDto : BaseSearchDto
    {
        public int? StudentStatus { get; set; }
        public int? GradeLevel { get; set; }
        public bool? Gender { get; set; }
    }
}
