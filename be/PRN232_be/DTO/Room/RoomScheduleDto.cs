using PRN232_be.Enums;

namespace PRN232_be.DTO.Room
{
    /// <summary>
    /// Lịch sử dụng phòng - trả về các ClassSchedule và ExamSchedule có dùng phòng này
    /// </summary>
    public class RoomScheduleDto
    {
        public int ScheduleId { get; set; }
        public string ScheduleType { get; set; } = string.Empty; // "ClassSchedule" | "ExamSchedule"
        public string? ClassName { get; set; }
        public string? SlotName { get; set; }
        public string? SlotTime { get; set; }  // "07:00 - 09:00"
        public DateTime? ScheduleDate { get; set; }
        public int Status { get; set; }
        public string? StatusName { get; set; }
        public string? Note { get; set; }
    }
}
