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
                // Force reset mật khẩu admin về "123456" để phục vụ việc test
                await userManager.RemovePasswordAsync(adminUser);
                await userManager.AddPasswordAsync(adminUser, "123456");

                // Đảm bảo user admin đã có role Admin
                if (!await userManager.IsInRoleAsync(adminUser, adminRoleName))
                {
                    await userManager.AddToRoleAsync(adminUser, adminRoleName);
                }
            }

            // 4. Cấp permissions cho vai trò Student
            var studentPermissions = new[]
            {
                Permissions.MyClass.MyClassPage,
                Permissions.StudentExam.StudentExamPage,
                Permissions.MyGrade.MyGradePage,
                Permissions.StudentHomework.StudentHomeworkPage,
                Permissions.StudentHomework.StudentHomework_View,
                Permissions.StudentHomework.StudentHomework_Submit,
                Permissions.StudentProgress.StudentProgressPage,
                Permissions.Attendance.Attendance_View,
                Permissions.LearningMaterial.LearningMaterial_View
            };
            await EnsureRolePermissionsAsync(roleManager, "Student", studentPermissions);

            // 4.5. Cấp permissions cho vai trò Parent
            var parentPermissions = new[]
            {
                Permissions.ParentStudent.ParentStudent_View,
                Permissions.ChildProgress.ChildProgressPage,
                Permissions.ChildSchedule.ChildSchedulePage
            };
            await EnsureRolePermissionsAsync(roleManager, "Parent", parentPermissions);

            // 4.6. Cấp permissions cho vai trò Teacher
            var teacherPermissions = new[]
            {
                Permissions.TeachingClass.TeachingClassPage,
                Permissions.TeachingSchedule.TeachingSchedulePage,
                Permissions.TeachingExam.TeachingExamPage,
                Permissions.Course.Course_View,
                Permissions.Class.Class_View,
                Permissions.Question.QuestionPage,
                Permissions.Question.Question_View,
                Permissions.Question.Question_Create,
                Permissions.Question.Question_Edit,
                Permissions.Question.Question_Delete,
                Permissions.QuestionCategory.QuestionCategoryPage,
                Permissions.QuestionCategory.QuestionCategory_View,
                Permissions.QuestionCategory.QuestionCategory_Create,
                Permissions.QuestionCategory.QuestionCategory_Edit,
                Permissions.QuestionCategory.QuestionCategory_Delete,
                Permissions.Attendance.AttendancePage,
                Permissions.Attendance.Attendance_View,
                Permissions.Attendance.Attendance_Create,
                Permissions.Attendance.Attendance_Edit,
                Permissions.Attendance.Attendance_SaveAttendance,
                Permissions.LearningMaterial.LearningMaterialPage,
                Permissions.LearningMaterial.LearningMaterial_View,
                Permissions.LearningMaterial.LearningMaterial_Create,
                Permissions.LearningMaterial.LearningMaterial_Edit,
                Permissions.LearningMaterial.LearningMaterial_Delete,
                Permissions.HomeworkManagement.HomeworkManagementPage,
                Permissions.HomeworkManagement.HomeworkManagement_View,
                Permissions.HomeworkManagement.HomeworkManagement_Create,
                Permissions.HomeworkManagement.HomeworkManagement_Edit,
                Permissions.HomeworkManagement.HomeworkManagement_Delete,
                Permissions.StudentGrade.StudentGradePage,
                Permissions.StudentGrade.StudentGrade_ViewSettings,
                Permissions.StudentGrade.StudentGrade_SaveGrade
            };
            await EnsureRolePermissionsAsync(roleManager, "Teacher", teacherPermissions);

            // 4.7. Cấp permissions cho Operation staff
            var opStaffPermissions = new[]
            {
                Permissions.Course.CoursePage, Permissions.Course.Course_View, Permissions.Course.Course_Create, Permissions.Course.Course_Edit, Permissions.Course.Course_Delete,
                Permissions.Class.ClassPage, Permissions.Class.Class_View, Permissions.Class.Class_Create, Permissions.Class.Class_Edit, Permissions.Class.Class_Delete, Permissions.Class.Class_Import,
                Permissions.Student.StudentPage, Permissions.Student.Student_View, Permissions.Student.Student_Create, Permissions.Student.Student_Edit, Permissions.Student.Student_Delete,
                Permissions.Teacher.TeacherPage, Permissions.Teacher.Teacher_View, Permissions.Teacher.Teacher_Create, Permissions.Teacher.Teacher_Edit, Permissions.Teacher.Teacher_Delete,
                Permissions.Room.RoomPage, Permissions.Room.Room_View, Permissions.Room.Room_Create, Permissions.Room.Room_Edit, Permissions.Room.Room_Delete,
                Permissions.StudentClass.StudentClassPage, Permissions.StudentClass.StudentClass_View, Permissions.StudentClass.StudentClass_Create, Permissions.StudentClass.StudentClass_Edit, Permissions.StudentClass.StudentClass_Delete,
                Permissions.Schedule.SchedulePage, Permissions.Timetable.TimetablePage,
                Permissions.Semester.SemesterPage, Permissions.Semester.Semester_View, Permissions.Semester.Semester_Create, Permissions.Semester.Semester_Edit, Permissions.Semester.Semester_Delete, Permissions.Semester.Semester_Scheduling,
                Permissions.StudentRegistration.StudentRegistrationPage, Permissions.StudentRegistration.StudentRegistration_View, Permissions.StudentRegistration.StudentRegistration_Create, Permissions.StudentRegistration.StudentRegistration_Edit, Permissions.StudentRegistration.StudentRegistration_Delete, Permissions.StudentRegistration.StudentRegistration_Import,
                Permissions.Exam.ExamPage, Permissions.Exam.Exam_View, Permissions.Exam.Exam_Create, Permissions.Exam.Exam_Edit, Permissions.Exam.Exam_Delete,
                Permissions.Question.QuestionPage, Permissions.Question.Question_View, Permissions.Question.Question_Create, Permissions.Question.Question_Edit, Permissions.Question.Question_Delete,
                Permissions.QuestionCategory.QuestionCategoryPage, Permissions.QuestionCategory.QuestionCategory_View, Permissions.QuestionCategory.QuestionCategory_Create, Permissions.QuestionCategory.QuestionCategory_Edit, Permissions.QuestionCategory.QuestionCategory_Delete,
                Permissions.StudentGrade.StudentGradePage, Permissions.StudentGrade.StudentGrade_ViewSettings, Permissions.StudentGrade.StudentGrade_SaveGrade
            };
            await EnsureRolePermissionsAsync(roleManager, "Operation staff", opStaffPermissions);

            // 4.8. Cấp permissions cho Academic staff
            await EnsureRolePermissionsAsync(roleManager, "Academic staff", opStaffPermissions);

            // 4.9. Cấp permissions cho Center manager
            await EnsureRolePermissionsAsync(roleManager, "Center manager", allPermissions);
        }

        private static async Task EnsureRolePermissionsAsync(RoleManager<IdentityRole> roleManager, string roleName, IEnumerable<string> permissions)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role == null)
            {
                role = new IdentityRole(roleName);
                await roleManager.CreateAsync(role);
                role = await roleManager.FindByNameAsync(roleName);
            }
            if (role == null) return;

            var existingClaims = await roleManager.GetClaimsAsync(role);
            var targetSet = permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Remove permissions no longer in target set for this role
            foreach (var claim in existingClaims.Where(c => c.Type.Equals("Permission", StringComparison.OrdinalIgnoreCase)))
            {
                if (!targetSet.Contains(claim.Value))
                {
                    await roleManager.RemoveClaimAsync(role, claim);
                }
            }

            var existingPermissionValues = existingClaims
                .Where(c => c.Type.Equals("Permission", StringComparison.OrdinalIgnoreCase))
                .Select(c => c.Value)
                .ToHashSet();

            foreach (var permission in permissions)
            {
                if (!existingPermissionValues.Contains(permission))
                {
                    await roleManager.AddClaimAsync(role, new Claim("Permission", permission));
                }
            }
        }
    }
}

