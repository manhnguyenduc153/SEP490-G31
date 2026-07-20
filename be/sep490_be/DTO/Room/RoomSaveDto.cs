using sep490_be.Enums;
using sep490_be.Helpers;

namespace sep490_be.DTO.Room
{
    public class RoomSaveDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int? Capacity { get; set; }
        public int Status { get; set; }
        public string? Building { get; set; }
        public string? Floor { get; set; }

        public string TextSearch => StringHelper.GenerateTextSearch(Code, Name, Building, Floor);
    }
}

