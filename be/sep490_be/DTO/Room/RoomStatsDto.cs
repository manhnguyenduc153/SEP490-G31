using sep490_be.DTO.Room;

namespace sep490_be.DTO.Room
{
    /// <summary>
    /// Thống kê tổng quan phòng học - dùng cho Overview Cards (BR)
    /// </summary>
    public class RoomStatsDto
    {
        public int TotalRooms { get; set; }
        public int AvailableRooms { get; set; }  // Active
        public int InUseRooms { get; set; }       // Đang trong lịch học (ngày hôm nay)
        public int MaintenanceRooms { get; set; } // Maintaince
    }
}

