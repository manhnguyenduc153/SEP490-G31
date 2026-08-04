using sep490_be.DTO;

namespace sep490_be.DTO.QuestionPassage
{
    public class QuestionPassageSearchDto : BaseSearchDto
    {
        public int? SkillType { get; set; }
        public int? CategoryId { get; set; }
    }
}
