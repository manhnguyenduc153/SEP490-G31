using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using FluentAssertions;
using sep490_be.DTO.LearningMaterial;
using sep490_be.Models;
using sep490_be.Repositories.Common;
using sep490_be.Repositories.Implementations;
using sep490_be.Services.Implementations;
using Xunit;

namespace sep490_be.Tests.Services
{
    public class LearningMaterialServiceTests
    {
        private static DbContextOptions<ApplicationDbContext> CreateNewContextOptions()
        {
            return new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
        }

        private static Mock<IHttpContextAccessor> GetMockHttpContextAccessor()
        {
            return new Mock<IHttpContextAccessor>();
        }

        private static LearningMaterialService CreateService(ApplicationDbContext context)
        {
            var uow = new UnitOfWork<ApplicationDbContext>(context);
            var repo = new LearningMaterialRepository(context, uow);
            return new LearningMaterialService(repo, context);
        }

        #region Normal Test Cases (Kiểm thử giá trị thông thường)

        [Fact]
        public async Task Normal_GetAllMaterialsAsync_AsAdmin_ShouldReturnAllMaterials()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                context.LearningMaterials.AddRange(
                    new LearningMaterial { Code = "MAT01", Name = "Material 1", Status = 1 },
                    new LearningMaterial { Code = "MAT02", Name = "Material 2", Status = 1 }
                );
                await context.SaveChangesAsync();
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateService(context);
                var response = await service.GetAllMaterialsAsync(
                    new LearningMaterialSearchDto { PageIndex = 1, PageSize = 10 },
                    "admin@test.com",
                    new List<string> { "Admin" }
                );

                // Assert
                response.Success.Should().BeTrue();
                response.Data.Should().NotBeNull();
                response.Data!.Items.Should().HaveCount(2);
            }
        }

        [Fact]
        public async Task Normal_GetAllMaterialsAsync_AsStudent_ShouldOnlyReturnEnrolledClassOrCourseMaterials()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            string studentEmail = "student@test.com";

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var student = new Student { Code = "ST01", Name = "Student 1", Email = studentEmail, Status = 1 };
                var courseEnrolled = new Course { Code = "C01", Name = "Course 1", Status = 1 };
                var courseNotEnrolled = new Course { Code = "C02", Name = "Course 2", Status = 1 };
                var classEnrolled = new Class { Code = "CL01", Name = "Class 1", Course = courseEnrolled, Status = 1 };
                var classNotEnrolled = new Class { Code = "CL02", Name = "Class 2", Course = courseNotEnrolled, Status = 1 };

                context.Students.Add(student);
                context.Courses.AddRange(courseEnrolled, courseNotEnrolled);
                context.Classes.AddRange(classEnrolled, classNotEnrolled);
                await context.SaveChangesAsync();

                // Enroll student to classEnrolled
                context.StudentClasses.Add(new StudentClass { StudentId = student.Id, ClassId = classEnrolled.Id, Status = 1 });

                // Materials
                context.LearningMaterials.AddRange(
                    new LearningMaterial { Code = "MAT_CL01", Name = "Class Material", ClassId = classEnrolled.Id, Status = 1 },
                    new LearningMaterial { Code = "MAT_C01", Name = "Course Material", CourseId = courseEnrolled.Id, Status = 1 },
                    new LearningMaterial { Code = "MAT_OTHER", Name = "Other Material", ClassId = classNotEnrolled.Id, Status = 1 }
                );
                await context.SaveChangesAsync();
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateService(context);
                var response = await service.GetAllMaterialsAsync(
                    new LearningMaterialSearchDto { PageIndex = 1, PageSize = 10 },
                    studentEmail,
                    new List<string> { "Student" }
                );

                // Assert
                response.Success.Should().BeTrue();
                response.Data!.Items.Should().HaveCount(2);
                response.Data.Items.Select(m => m.Code).Should().Contain(new[] { "MAT_CL01", "MAT_C01" });
                response.Data.Items.Select(m => m.Code).Should().NotContain("MAT_OTHER");
            }
        }

        [Fact]
        public async Task Normal_GetMaterialByIdAsync_WhenExists_ShouldReturnMaterial()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int matId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var mat = new LearningMaterial { Code = "MAT01", Name = "Material 1", Status = 1 };
                context.LearningMaterials.Add(mat);
                await context.SaveChangesAsync();
                matId = mat.Id;
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateService(context);
                var response = await service.GetMaterialByIdAsync(matId);

                // Assert
                response.Success.Should().BeTrue();
                response.Data.Should().NotBeNull();
                response.Data!.Code.Should().Be("MAT01");
            }
        }

        [Fact]
        public async Task Normal_CreateMaterialAsync_AsTeacher_ShouldAutoAssignUploadedBy()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            string teacherEmail = "teacher@test.com";
            int teacherId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var teacher = new Teacher { Code = "TC01", Name = "Teacher 1", Email = teacherEmail, Status = 1 };
                context.Teachers.Add(teacher);
                await context.SaveChangesAsync();
                teacherId = teacher.Id;
            }

            var dto = new LearningMaterialSaveDto
            {
                Code = "MAT_TC",
                Name = "Teacher Material",
                Status = 1
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateService(context);
                var response = await service.CreateMaterialAsync(dto, teacherEmail);

                // Assert
                response.Success.Should().BeTrue();
                response.Data!.UploadedBy.Should().Be(teacherId);

                var materialInDb = await context.LearningMaterials.FirstOrDefaultAsync(m => m.Code == "MAT_TC");
                materialInDb.Should().NotBeNull();
                materialInDb!.UploadedBy.Should().Be(teacherId);
            }
        }

        [Fact]
        public async Task Normal_EditMaterialAsync_AsTeacherEditingOwnMaterial_ShouldSucceed()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            string teacherEmail = "teacher@test.com";
            int teacherId;
            int matId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var teacher = new Teacher { Code = "TC01", Name = "Teacher 1", Email = teacherEmail, Status = 1 };
                context.Teachers.Add(teacher);
                await context.SaveChangesAsync();
                teacherId = teacher.Id;

                var mat = new LearningMaterial { Code = "MAT01", Name = "Material 1", UploadedBy = teacherId, Status = 1 };
                context.LearningMaterials.Add(mat);
                await context.SaveChangesAsync();
                matId = mat.Id;
            }

            var dto = new LearningMaterialSaveDto
            {
                Id = matId,
                Code = "MAT01_UPD",
                Name = "Material 1 Updated",
                Status = 1
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateService(context);
                var response = await service.EditMaterialAsync(dto, teacherEmail, new List<string> { "Teacher" });

                // Assert
                response.Success.Should().BeTrue();
                response.Data!.Code.Should().Be("MAT01_UPD");
            }
        }

        [Fact]
        public async Task Normal_DeleteMaterialAsync_AsTeacherDeletingOwnMaterial_ShouldSucceed()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            string teacherEmail = "teacher@test.com";
            int teacherId;
            int matId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var teacher = new Teacher { Code = "TC01", Name = "Teacher 1", Email = teacherEmail, Status = 1 };
                context.Teachers.Add(teacher);
                await context.SaveChangesAsync();
                teacherId = teacher.Id;

                var mat = new LearningMaterial { Code = "MAT01", Name = "Material 1", UploadedBy = teacherId, Status = 1 };
                context.LearningMaterials.Add(mat);
                await context.SaveChangesAsync();
                matId = mat.Id;
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateService(context);
                var response = await service.DeleteMaterialAsync(matId, teacherEmail, new List<string> { "Teacher" });

                // Assert
                response.Success.Should().BeTrue();
                response.Data.Should().BeTrue();

                var deletedMat = await context.LearningMaterials.IgnoreQueryFilters().FirstOrDefaultAsync(m => m.Id == matId);
                deletedMat.Should().NotBeNull();
                deletedMat!.IsDeleted.Should().BeTrue();
            }
        }

        [Fact]
        public async Task Normal_DeactiveMaterialAsync_AsTeacherDeactivatingOwnMaterial_ShouldSucceed()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            string teacherEmail = "teacher@test.com";
            int teacherId;
            int matId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var teacher = new Teacher { Code = "TC01", Name = "Teacher 1", Email = teacherEmail, Status = 1 };
                context.Teachers.Add(teacher);
                await context.SaveChangesAsync();
                teacherId = teacher.Id;

                var mat = new LearningMaterial { Code = "MAT01", Name = "Material 1", UploadedBy = teacherId, Status = 1 };
                context.LearningMaterials.Add(mat);
                await context.SaveChangesAsync();
                matId = mat.Id;
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateService(context);
                var response = await service.DeactiveMaterialAsync(matId, teacherEmail, new List<string> { "Teacher" });

                // Assert
                response.Success.Should().BeTrue();
                response.Data.Should().BeTrue();

                var deactivatedMat = await context.LearningMaterials.IgnoreQueryFilters().FirstOrDefaultAsync(m => m.Id == matId);
                deactivatedMat.Should().NotBeNull();
                deactivatedMat!.IsDeleted.Should().BeTrue();
            }
        }

        #endregion

        #region Boundary Test Cases (Kiểm thử giá trị biên)

        [Fact]
        public async Task Boundary_CreateMaterialAsync_WithMaxStringLengths_ShouldSucceed()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            var dto = new LearningMaterialSaveDto
            {
                Code = new string('C', 50),
                Name = new string('N', 200),
                Title = new string('T', 250),
                Description = new string('D', 1000),
                FileUrl = new string('U', 500),
                FileType = new string('F', 50),
                Status = 1
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateService(context);
                var response = await service.CreateMaterialAsync(dto, "admin@test.com");

                // Assert
                response.Success.Should().BeTrue();
                response.Data!.Code.Should().HaveLength(50);
                response.Data.Name.Should().HaveLength(200);
                response.Data.Title.Should().HaveLength(250);
                response.Data.Description.Should().HaveLength(1000);
                response.Data.FileUrl.Should().HaveLength(500);
                response.Data.FileType.Should().HaveLength(50);
            }
        }

        #endregion

        #region Abnormal Test Cases (Kiểm thử giá trị bất thường / Lỗi)

        [Fact]
        public async Task Abnormal_GetAllMaterialsAsync_AsStudentButStudentNotFound_ShouldReturnNotFound()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateService(context);
                var response = await service.GetAllMaterialsAsync(
                    new LearningMaterialSearchDto { PageIndex = 1, PageSize = 10 },
                    "nonexistent_student@test.com",
                    new List<string> { "Student" }
                );

                // Assert
                response.Success.Should().BeFalse();
                response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
                response.Message.Should().Be("ERR_STUDENT_NOT_FOUND");
            }
        }

        [Fact]
        public async Task Abnormal_GetMaterialByIdAsync_WhenDoesNotExist_ShouldReturnNotFound()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateService(context);
                var response = await service.GetMaterialByIdAsync(9999);

                // Assert
                response.Success.Should().BeFalse();
                response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
                response.Message.Should().Be("ERR_LEARNING_MATERIAL_NOT_FOUND");
            }
        }

        [Fact]
        public async Task Abnormal_CreateMaterialAsync_WithDuplicateCode_ShouldReturnBadRequest()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                context.LearningMaterials.Add(new LearningMaterial { Code = "DUP_C", Name = "Existing", Status = 1 });
                await context.SaveChangesAsync();
            }

            var dto = new LearningMaterialSaveDto { Code = "DUP_C", Name = "Duplicate Code" };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateService(context);
                var response = await service.CreateMaterialAsync(dto, "admin@test.com");

                // Assert
                response.Success.Should().BeFalse();
                response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
                response.Message.Should().Be("ERR_CODE_DUPLICATE");
            }
        }

        [Fact]
        public async Task Abnormal_EditMaterialAsync_AsTeacherEditingOtherTeacherMaterial_ShouldReturnForbidden()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            string teacherEmail1 = "teacher1@test.com";
            string teacherEmail2 = "teacher2@test.com";
            int teacherId1;
            int teacherId2;
            int matId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var t1 = new Teacher { Code = "TC01", Name = "Teacher 1", Email = teacherEmail1, Status = 1 };
                var t2 = new Teacher { Code = "TC02", Name = "Teacher 2", Email = teacherEmail2, Status = 1 };
                context.Teachers.AddRange(t1, t2);
                await context.SaveChangesAsync();
                teacherId1 = t1.Id;
                teacherId2 = t2.Id;

                // Material uploaded by Teacher 1
                var mat = new LearningMaterial { Code = "MAT01", Name = "Material 1", UploadedBy = teacherId1, Status = 1 };
                context.LearningMaterials.Add(mat);
                await context.SaveChangesAsync();
                matId = mat.Id;
            }

            var dto = new LearningMaterialSaveDto
            {
                Id = matId,
                Code = "MAT01_UPD",
                Name = "Material 1 Updated",
                Status = 1
            };

            // Act: Teacher 2 tries to edit Teacher 1's material
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateService(context);
                var response = await service.EditMaterialAsync(dto, teacherEmail2, new List<string> { "Teacher" });

                // Assert
                response.Success.Should().BeFalse();
                response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
                response.Message.Should().Be("ERR_FORBIDDEN_EDIT_OTHER_MATERIAL");
            }
        }

        [Fact]
        public async Task Abnormal_DeleteMaterialAsync_AsTeacherDeletingOtherTeacherMaterial_ShouldReturnForbidden()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            string teacherEmail1 = "teacher1@test.com";
            string teacherEmail2 = "teacher2@test.com";
            int teacherId1;
            int teacherId2;
            int matId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var t1 = new Teacher { Code = "TC01", Name = "Teacher 1", Email = teacherEmail1, Status = 1 };
                var t2 = new Teacher { Code = "TC02", Name = "Teacher 2", Email = teacherEmail2, Status = 1 };
                context.Teachers.AddRange(t1, t2);
                await context.SaveChangesAsync();
                teacherId1 = t1.Id;
                teacherId2 = t2.Id;

                // Material uploaded by Teacher 1
                var mat = new LearningMaterial { Code = "MAT01", Name = "Material 1", UploadedBy = teacherId1, Status = 1 };
                context.LearningMaterials.Add(mat);
                await context.SaveChangesAsync();
                matId = mat.Id;
            }

            // Act: Teacher 2 tries to delete Teacher 1's material
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateService(context);
                var response = await service.DeleteMaterialAsync(matId, teacherEmail2, new List<string> { "Teacher" });

                // Assert
                response.Success.Should().BeFalse();
                response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
                response.Message.Should().Be("ERR_FORBIDDEN_DELETE_OTHER_MATERIAL");
            }
        }

        #endregion
    }
}
