using sep490_be.DTO.Common;

namespace sep490_be.DTO.LearningMaterial
{
    public class LearningMaterialSearchDto : BaseSearchDto
    {
        public int? ClassId { get; set; }
        public int? CourseId { get; set; }
        public int? ScheduleId { get; set; }
        public int? UploadedBy { get; set; }
    }
}

