
namespace PRN232_be.Enums
{
    public enum RoomStatus
    {
        [StringValue("Active")]
        Active = 1,
        [StringValue("Inactive")]
        Inactive = 2,
        [StringValue("Maintenance")]
        Maintaince = 3
    }

    public enum RoomType
    {
        [StringValue("Theory")]
        Theory = 1,
        [StringValue("Practice")]
        Pratice = 2,
    }
}
