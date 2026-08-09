namespace sep490_be.DTO.QuestionPassage
{
    // Read-only passage snapshot embedded in an exam's questions so students can see the
    // passage/prompt/recording they're answering without needing QuestionPassage.View permission
    // (that admin/teacher endpoint also returns every answer's IsCorrect, which students must never see).
    public class QuestionPassageSummaryDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Content { get; set; }
        public string? AudioUrl { get; set; }
        public string? AttachmentUrl { get; set; }
        public int SkillType { get; set; }
    }
}
