using Microsoft.AspNetCore.Authorization;

namespace sep490_be.Helpers.Authorization
{
    public class HasPermissionAttribute : AuthorizeAttribute
    {
        public HasPermissionAttribute(string permission) : base(permission)
        {
        }
    }
}

