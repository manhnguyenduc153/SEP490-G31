namespace sep490_be.DTO.Class
{
    public class AutoScheduleSemesterRequestDto
    {
        public int SemesterId { get; set; }
        public int MaxClassSize { get; set; } = 15;
        public int MinClassSize { get; set; } = 5;
        public AutoScheduleConstraintDto Constraints { get; set; } = new AutoScheduleConstraintDto();
    }
}

