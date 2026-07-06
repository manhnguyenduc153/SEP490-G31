using sep490_be.DTO;

namespace sep490_be.DTO.Class
{
    public class ClassSearchDto : BaseSearchDto
    {
        public int? CourseId { get; set; }
        public int? TeacherId { get; set; }
    }
}

