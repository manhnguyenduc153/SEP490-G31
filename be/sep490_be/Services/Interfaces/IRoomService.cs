using sep490_be.DTO;
using sep490_be.DTO.Room;

namespace sep490_be.Services.Interfaces
{
    public interface IRoomService
    {
        /// <summary>Lấy danh sách phòng học có phân trang và tìm kiếm</summary>
        Task<ApiResponse<PagingResponse<RoomDto>>> GetAllAsync(RoomSearchDto searchDto);

        /// <summary>Lấy chi tiết một phòng học theo Id</summary>
        Task<ApiResponse<RoomDto>> GetByIdAsync(int id);

        /// <summary>Tạo phòng học mới (BR-11: Tên phải unique)</summary>
        Task<ApiResponse<RoomDto>> CreateAsync(RoomSaveDto dto);

        /// <summary>Cập nhật thông tin phòng học (BR-11: Tên không được trùng phòng khác)</summary>
        Task<ApiResponse<RoomDto>> EditAsync(RoomSaveDto dto);

        /// <summary>Xóa mềm phòng học</summary>
        Task<ApiResponse<bool>> DeleteAsync(int id);

        /// <summary>Vô hiệu hóa phòng học - chuyển sang Inactive (BR-08)</summary>
        Task<ApiResponse<bool>> DeactiveAsync(int id);

        /// <summary>Thống kê tổng quan phòng học (Total / Available / InUse / Maintenance)</summary>
        Task<ApiResponse<RoomStatsDto>> GetStatsAsync();

        /// <summary>Lịch sử dụng của phòng học (ClassSchedule + ExamSchedule)</summary>
        Task<ApiResponse<PagingResponse<RoomScheduleDto>>> GetScheduleAsync(int roomId, BaseSearchDto searchDto);

        /// <summary>Lấy danh sách phòng học khả dụng (thỏa mãn sức chứa và không bị trùng lịch)</summary>
        Task<ApiResponse<List<RoomDto>>> GetAvailableRoomsAsync(AvailableRoomFilterDto filterDto);
    }
}

