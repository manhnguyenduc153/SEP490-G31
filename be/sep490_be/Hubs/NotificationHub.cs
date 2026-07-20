using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace sep490_be.Hubs
{
    public class NotificationHub : Hub
    {
        public const string AdminGroup = "admin-group";

        private bool IsAdminUser(System.Security.Claims.ClaimsPrincipal? user)
        {
            if (user == null) return false;
            
            var roleClaims = user.FindAll(c => c.Type == System.Security.Claims.ClaimTypes.Role || c.Type == "role")
                                 .Select(c => c.Value);

            return roleClaims.Any(r => 
                string.Equals(r, "Admin", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r, "Quản lý trung tâm", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r, "Ban vận hành", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r, "Ban chuyên môn", System.StringComparison.OrdinalIgnoreCase)
            );
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.UserIdentifier;
            var userName = Context.User?.Identity?.Name;
            var roleClaims = Context.User?.FindAll(c => c.Type == System.Security.Claims.ClaimTypes.Role || c.Type == "role")
                                 .Select(c => c.Value) ?? System.Linq.Enumerable.Empty<string>();

            System.Console.WriteLine($"[SignalR Connect] ConnectionId: {Context.ConnectionId}, UserIdentifier: {userId}, UserName: {userName}, IsAuthenticated: {Context.User?.Identity?.IsAuthenticated}, Roles: {string.Join(", ", roleClaims)}");

            if (IsAdminUser(Context.User))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, AdminGroup);
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(System.Exception? exception)
        {
            if (IsAdminUser(Context.User))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, AdminGroup);
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}
