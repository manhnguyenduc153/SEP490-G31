using System.Security.Claims;

namespace sep490_be.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        public static int GetUserId(this ClaimsPrincipal user)
        {
            if (user == null) return 0;

            // Try "id" claim first (common in some setups or custom)
            var claim = user.FindFirst("id");
            if (claim != null && int.TryParse(claim.Value, out int id))
            {
                return id;
            }

            // Try standard NameIdentifier
            claim = user.FindFirst(ClaimTypes.NameIdentifier);
            if (claim != null && int.TryParse(claim.Value, out id))
            {
                return id;
            }

            // Try "UserId" claim
            claim = user.FindFirst("UserId");
            if (claim != null && int.TryParse(claim.Value, out id))
            {
                return id;
            }

            return 0;
        }

        public static string? GetUserEmail(this ClaimsPrincipal user)
        {
            if (user == null) return null;

            // Try Email claim
            var claim = user.FindFirst(ClaimTypes.Email);
            if (claim != null)
            {
                return claim.Value;
            }

            // Try "email" claim (lowercase)
            claim = user.FindFirst("email");
            if (claim != null)
            {
                return claim.Value;
            }

            return null;
        }

        public static string? GetUsername(this ClaimsPrincipal user)
        {
            if (user == null) return null;

            // Try Name claim
            var claim = user.FindFirst(ClaimTypes.Name);
            if (claim != null)
            {
                return claim.Value;
            }

            // Try "username" claim
            claim = user.FindFirst("username");
            if (claim != null)
            {
                return claim.Value;
            }

            return null;
        }

        public static string? GetUserRole(this ClaimsPrincipal user)
        {
            if (user == null) return null;

            // Try Role claim
            var claim = user.FindFirst(ClaimTypes.Role);
            if (claim != null)
            {
                return claim.Value;
            }

            // Try "role" claim (lowercase)
            claim = user.FindFirst("role");
            if (claim != null)
            {
                return claim.Value;
            }

            return null;
        }

        public static bool IsInRole(this ClaimsPrincipal user, string role)
        {
            if (user == null || string.IsNullOrEmpty(role)) return false;

            var userRole = user.GetUserRole();
            return userRole?.Equals(role, StringComparison.OrdinalIgnoreCase) ?? false;
        }

        public static bool IsAdmin(this ClaimsPrincipal user)
        {
            return user.IsInRole("ADMIN");
        }

        public static bool IsSeller(this ClaimsPrincipal user)
        {
            return user.IsInRole("SELLER");
        }

        public static bool IsCustomer(this ClaimsPrincipal user)
        {
            return user.IsInRole("BUYER");
        }
    }
}
