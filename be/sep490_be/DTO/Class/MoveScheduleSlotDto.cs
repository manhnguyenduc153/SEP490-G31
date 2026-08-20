using System;

namespace sep490_be.DTO.Class
{
    public class MoveScheduleSlotDto
    {
        public DateTime NewDate { get; set; }
        public int? NewSlotIndex { get; set; } // 0..4 (FixedTimeSlot index)
        public int? NewSlotId { get; set; }    // TimeSlot ID in DB
        public int? TeacherId { get; set; }
        public int? RoomId { get; set; }
        public bool ForceOverride { get; set; } = false;
    }
}
