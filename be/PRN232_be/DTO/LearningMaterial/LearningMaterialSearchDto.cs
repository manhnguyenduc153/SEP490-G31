using PRN232_be.DTO.Common;

namespace PRN232_be.DTO.LearningMaterial
{
    public class LearningMaterialSearchDto : BaseSearchDto
    {
        public int? ClassId { get; set; }
        public int? CourseId { get; set; }
        public int? ScheduleId { get; set; }
        public int? UploadedBy { get; set; }
    }
}
