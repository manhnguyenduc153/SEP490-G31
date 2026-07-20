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

            // 4. Tạo vai trò Student nếu chưa tồn tại và cấp permissions cần thiết
            const string studentRoleName = "Student";
            var studentRole = await roleManager.FindByNameAsync(studentRoleName);
            if (studentRole == null)
            {
                studentRole = new IdentityRole(studentRoleName);
                await roleManager.CreateAsync(studentRole);
                studentRole = await roleManager.FindByNameAsync(studentRoleName);
            }

            // Danh sách permissions dành cho Student
            var studentPermissions = new List<string>
            {
                Permissions.Class.Class_StudentView,
                Permissions.Homework.Homework_View,
                Permissions.Homework.Homework_Create,
                Permissions.Attendance.Attendance_View,
                Permissions.StudentGrade.StudentGrade_View,
                Permissions.ClassSchedule.ClassSchedule_View,
                Permissions.Notification.Notification_View,
                Permissions.LearningMaterial.LearningMaterial_View,
            };

            var existingStudentClaims = await roleManager.GetClaimsAsync(studentRole!);
            var existingStudentPermissions = existingStudentClaims
                .Where(c => c.Type.Equals("Permission", System.StringComparison.OrdinalIgnoreCase))
                .Select(c => c.Value)
                .ToList();

            foreach (var perm in studentPermissions)
            {
                if (!existingStudentPermissions.Contains(perm))
                {
                    await roleManager.AddClaimAsync(studentRole!, new Claim("Permission", perm));
                }
            }

            // 5. Seed các vai trò bổ sung yêu cầu (Học sinh, Giáo viên, Ban vận hành, Ban chuyên môn, Quản lý trung tâm, Phụ huynh)
            var newRolesToSeed = new List<string>
            {
                "Học sinh",
                "Giáo viên",
                "Ban vận hành",
                "Ban chuyên môn",
                "Quản lý trung tâm",
                "Phụ huynh"
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

