using PRN232_be.Enums;
using PRN232_be.Helpers;

namespace PRN232_be.DTO.Room
{
    public class RoomSaveDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int? Capacity { get; set; }
        public int Status { get; set; }
        public RoomType RoomType { get; set; }
        public string? Building { get; set; }
        public string? Floor { get; set; }

        /// <summary>
        /// URL ảnh phòng học. Client gọi POST /api/upload/image trước để lấy URL, rồi truyền vào đây.
        /// </summary>
        public string? Image { get; set; }

        public string TextSearch => StringHelper.GenerateTextSearch(Code, Name, Building, Floor);
    }
}
