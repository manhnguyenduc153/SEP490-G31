using System;

namespace sep490_be.DTO.Room
{
    public class AvailableRoomFilterDto
    {
        public int? MinCapacity { get; set; }
        public int? ClassId { get; set; }
        public DateTime? Date { get; set; }
        public int? SlotIndex { get; set; } // 0..4 FixedTimeSlot index
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? DayOfWeek { get; set; }
        public int? ExcludeScheduleId { get; set; }
        public int? ExcludeClassId { get; set; }
        public string? WeeklySchedulesJson { get; set; }
    }
}
