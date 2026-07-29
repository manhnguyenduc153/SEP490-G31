using sep490_be.DTO;

namespace sep490_be.DTO.Question
{
    public class QuestionSearchDto : BaseSearchDto
    {
        public int? DifficultyLevel { get; set; }
        public int? CategoryId { get; set; }
        public int? QuestionType { get; set; }
        public int? SkillType { get; set; }
    }
}

