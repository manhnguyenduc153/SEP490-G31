using PRN232_be.DTO;
using PRN232_be.DTO.Room;

namespace PRN232_be.Services.Interfaces
{
    public interface IRoomService
    {
        Task<ApiResponse<PagingResponse<RoomDto>>> GetAllAsync(RoomSearchDto searchDto);
        Task<ApiResponse<RoomDto>> GetByIdAsync(int id);
    }
}
