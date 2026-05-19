using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace PRN232_be.Helpers.Authorization
{
    public class PermissionPolicyProvider : DefaultAuthorizationPolicyProvider
    {
        public PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : base(options)
        {
        }

        public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
        {
            // Lấy Policy mặc định nếu tồn tại
            var policy = await base.GetPolicyAsync(policyName);
            if (policy != null)
            {
                return policy;
            }

            // Nếu Policy chưa tồn tại, tự động tạo mới dựa trên tên Permission
            return new AuthorizationPolicyBuilder()
                .AddRequirements(new PermissionRequirement(policyName))
                .Build();
        }
    }
}
