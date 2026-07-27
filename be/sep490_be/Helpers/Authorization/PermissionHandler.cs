using Microsoft.AspNetCore.Authorization;

using System.Security.Claims;

namespace sep490_be.Helpers.Authorization
{
    public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
        {
            if (context.User == null)
            {
                return Task.CompletedTask;
            }

            // Admin luôn có toàn quyền, kể cả khi JWT được phát trước lúc có permission mới.
            var isAdmin = context.User.IsInRole("Admin") ||
                context.User.Claims.Any(c =>
                    (c.Type == ClaimTypes.Role || c.Type.Equals("role", StringComparison.OrdinalIgnoreCase)) &&
                    c.Value.Equals("Admin", StringComparison.OrdinalIgnoreCase));

            if (isAdmin)
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            // Kiểm tra xem context.User có chứa Claim loại "Permission" và giá trị khớp với yêu cầu hay không
            var requiredPerms = requirement.Permission.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var userPerms = context.User.Claims
                .Where(c => c.Type.Equals("Permission", StringComparison.OrdinalIgnoreCase))
                .Select(c => c.Value)
                .ToList();

            var hasPermission = requiredPerms.Any(rp => userPerms.Any(up => up.Equals(rp, StringComparison.OrdinalIgnoreCase)));

            if (hasPermission)
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}

