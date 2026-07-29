using sep490_be.DTO;

namespace sep490_be.DTO.QuestionCategory
{
    public class QuestionCategorySearchDto : BaseSearchDto
    {
        public int? CourseId { get; set; }
    }
}

