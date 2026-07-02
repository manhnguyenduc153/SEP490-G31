using PRN232_be.DTO;

namespace PRN232_be.DTO.Question
{
    public class QuestionSearchDto : BaseSearchDto
    {
        public int? DifficultyLevel { get; set; }
        public int? CategoryId { get; set; }
        public int? QuestionType { get; set; }
    }
}
