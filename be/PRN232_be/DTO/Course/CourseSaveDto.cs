using PRN232_be.Helpers;

namespace PRN232_be.DTO.Course
{
    public class CourseSaveDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Status { get; set; }
        public int? Duration { get; set; }
        public double? Price { get; set; }
        public string? Description { get; set; }

        public string TextSearch => StringHelper.GenerateTextSearch(Code, Name, Description);
    }
}
