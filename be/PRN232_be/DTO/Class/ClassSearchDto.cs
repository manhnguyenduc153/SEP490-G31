using PRN232_be.DTO;

namespace PRN232_be.DTO.Class
{
    public class ClassSearchDto : BaseSearchDto
    {
        public int? CourseId { get; set; }
        public int? TeacherId { get; set; }
    }
}
