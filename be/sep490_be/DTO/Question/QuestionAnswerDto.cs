namespace sep490_be.DTO.Question
{
    public class QuestionAnswerDto
    {
        public int Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
    }
}

