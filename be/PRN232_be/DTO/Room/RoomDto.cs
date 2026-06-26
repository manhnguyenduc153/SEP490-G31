namespace PRN232_be.DTO.Room
{
    public class RoomDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int? Capacity { get; set; }
        public int Status { get; set; }
    }
}
