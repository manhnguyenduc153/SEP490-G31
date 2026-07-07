using sep490_be.Helpers;

namespace sep490_be.DTO.QuestionCategory
{
    public class QuestionCategorySaveDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }

        public string TextSearch => StringHelper.GenerateTextSearch(Code, Name, Description);
    }
}

