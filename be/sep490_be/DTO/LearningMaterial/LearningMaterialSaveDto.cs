using sep490_be.Helpers;

namespace sep490_be.DTO.LearningMaterial
{
    public class LearningMaterialSaveDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int? ClassId { get; set; }
        public int? ScheduleId { get; set; }
        public int? UploadedBy { get; set; }
        public int? CourseId { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? FileUrl { get; set; }
        public string? FileType { get; set; }
        public int Status { get; set; }

        public string TextSearch => StringHelper.GenerateTextSearch(Code, Name, Title, Description);
    }
}

