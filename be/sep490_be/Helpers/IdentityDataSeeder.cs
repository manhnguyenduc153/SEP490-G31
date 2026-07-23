using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using sep490_be.DTO.Common;

namespace sep490_be.Helpers
{
    public static class IdentityDataSeeder
    {
        public static async Task SeedDataAsync(IServiceProvider services)
        {
            var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

            // 1. Tạo vai trò Admin nếu chưa tồn tại
            const string adminRoleName = "Admin";
            var adminRole = await roleManager.FindByNameAsync(adminRoleName);
            if (adminRole == null)
            {
                adminRole = new IdentityRole(adminRoleName);
                await roleManager.CreateAsync(adminRole);
            }

            // 2. Đồng bộ các Claims (Permissions) của Role Admin
            var allPermissions = Permissions.GetAllPermissions();
            var existingClaims = await roleManager.GetClaimsAsync(adminRole);

            // Xóa các Claim cũ không còn tồn tại trong Code
            foreach (var claim in existingClaims)
            {
                if (claim.Type.Equals("Permission", System.StringComparison.OrdinalIgnoreCase) && !allPermissions.Contains(claim.Value))
                {
                    await roleManager.RemoveClaimAsync(adminRole, claim);
                }
            }

            // Thêm các Claim mới có trong Code nhưng chưa có trong DB
            var existingPermissionValues = existingClaims
                .Where(c => c.Type.Equals("Permission", System.StringComparison.OrdinalIgnoreCase))
                .Select(c => c.Value)
                .ToList();

            foreach (var permission in allPermissions)
            {
                if (!existingPermissionValues.Contains(permission))
                {
                    await roleManager.AddClaimAsync(adminRole, new Claim("Permission", permission));
                }
            }

            // Chuyển quyền xem điểm cũ sang hai quyền chuyên biệt mà không làm mất quyền của các role hiện có.
            const string legacyStudentGradeView = "StudentGrade.View";
            foreach (var role in roleManager.Roles.ToList())
            {
                var roleClaims = await roleManager.GetClaimsAsync(role);
                var legacyClaim = roleClaims.FirstOrDefault(claim =>
                    claim.Type.Equals("Permission", StringComparison.OrdinalIgnoreCase) &&
                    claim.Value.Equals(legacyStudentGradeView, StringComparison.OrdinalIgnoreCase));

                if (legacyClaim == null) continue;

                var replacementPermission = role.Name?.Equals("Student", StringComparison.OrdinalIgnoreCase) == true
                    ? Permissions.StudentGrade.StudentGrade_ViewOwnGrades
                    : Permissions.StudentGrade.StudentGrade_ViewSettings;

                if (!roleClaims.Any(claim =>
                    claim.Type.Equals("Permission", StringComparison.OrdinalIgnoreCase) &&
                    claim.Value.Equals(replacementPermission, StringComparison.OrdinalIgnoreCase)))
                {
                    await roleManager.AddClaimAsync(role, new Claim("Permission", replacementPermission));
                }

                await roleManager.RemoveClaimAsync(role, legacyClaim);
            }

            // 3. Tạo tài khoản admin nếu chưa tồn tại
            const string adminUsername = "admin";
            var adminUser = await userManager.FindByNameAsync(adminUsername);
            if (adminUser == null)
            {
                adminUser = new IdentityUser
                {
                    UserName = adminUsername,
                    Email = "admin@example.com",
                    EmailConfirmed = true
                };
                
                var createResult = await userManager.CreateAsync(adminUser, "123456");
                if (createResult.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, adminRoleName);
                }
                else
                {
                    var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                    throw new Exception($"Không thể tạo tài khoản admin: {errors}");
                }
            }
            else
            {
                // Đảm bảo user admin đã có role Admin
                if (!await userManager.IsInRoleAsync(adminUser, adminRoleName))
                {
                    await userManager.AddToRoleAsync(adminUser, adminRoleName);
                }
            }

            // 5. Seed các vai trò bổ sung yêu cầu (Student, Teacher, Parent, Operation staff, Academic staff, Center manager)
            var newRolesToSeed = new List<string>
            {
                "Student",
                "Teacher",
                "Parent",
                "Operation staff",
                "Academic staff",
                "Center manager"
            };

            foreach (var roleName in newRolesToSeed)
            {
                var role = await roleManager.FindByNameAsync(roleName);
                if (role == null)
                {
                    role = new IdentityRole(roleName);
                    await roleManager.CreateAsync(role);
                }
            }
        }
    }
}

