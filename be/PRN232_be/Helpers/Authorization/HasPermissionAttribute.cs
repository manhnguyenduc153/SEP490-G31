using Microsoft.AspNetCore.Authorization;

namespace PRN232_be.Helpers.Authorization
{
    public class HasPermissionAttribute : AuthorizeAttribute
    {
        public HasPermissionAttribute(string permission) : base(permission)
        {
        }
    }
}
