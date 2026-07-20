using Microsoft.AspNetCore.SignalR;

namespace sep490_be.Hubs
{
    public class NameUserIdProvider : IUserIdProvider
    {
        public string? GetUserId(HubConnectionContext connection)
        {
            var user = connection.User;
            if (user == null) return null;

            // Fallback chain for identifying the user name/email
            var userId = user.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value 
                         ?? user.FindFirst("unique_name")?.Value 
                         ?? user.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                         ?? user.Identity?.Name;

            return userId?.Trim().ToLowerInvariant();
        }
    }
}
