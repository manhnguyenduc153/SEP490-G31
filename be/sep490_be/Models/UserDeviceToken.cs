using System;
using Microsoft.AspNetCore.Identity;
using sep490_be.Models.BaseEntities;

namespace sep490_be.Models
{
    public class UserDeviceToken : StandardEntity<int>
    {
        public string UserId { get; set; } = null!;
        public string FcmToken { get; set; } = null!;
        public string? DeviceType { get; set; } // "Android", "iOS", etc.
        public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;

        public virtual IdentityUser User { get; set; } = null!;
    }
}
