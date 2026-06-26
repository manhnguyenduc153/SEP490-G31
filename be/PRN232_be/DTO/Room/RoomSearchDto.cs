using PRN232_be.Enums;

namespace PRN232_be.DTO.Room
{
    public class RoomSearchDto : BaseSearchDto
    {
        /// <summary>Lọc theo loại phòng (Theory / Pratice)</summary>
        public RoomType? RoomType { get; set; }

        /// <summary>Lọc theo tòa nhà</summary>
        public string? Building { get; set; }

        /// <summary>Lọc theo sức chứa tối thiểu</summary>
        public int? MinCapacity { get; set; }
    }
}
