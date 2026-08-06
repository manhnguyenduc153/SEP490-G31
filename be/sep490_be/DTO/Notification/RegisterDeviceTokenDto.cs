namespace sep490_be.DTO.Notification
{
    public class RegisterDeviceTokenDto
    {
        public string FcmToken { get; set; } = null!;
        public string? DeviceType { get; set; }
    }
}
