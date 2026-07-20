using sep490_be.Enums;
using sep490_be.DTO;

namespace sep490_be.DTO.Room
{
    public class RoomSearchDto : BaseSearchDto
    {
        /// <summary>Lọc theo tòa nhà</summary>
        public string? Building { get; set; }

        /// <summary>Lọc theo sức chứa tối thiểu</summary>
        public int? MinCapacity { get; set; }
    }
}

