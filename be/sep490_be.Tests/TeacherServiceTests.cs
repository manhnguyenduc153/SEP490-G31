using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using sep490_be.DTO.Teacher;
using sep490_be.Enums;
using sep490_be.Models;
using sep490_be.Repositories.Common;
using sep490_be.Repositories.Implementations;
using sep490_be.Services.Implementations;

namespace sep490_be.Tests.Services
{
    public class TeacherServiceTests
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

        private static (UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager) CreateIdentityManagers(ApplicationDbContext context)
        {
            var userStore = new UserStore<IdentityUser>(context);
            var optionsAccessorMock = new Mock<Microsoft.Extensions.Options.IOptions<IdentityOptions>>();
            optionsAccessorMock.Setup(o => o.Value).Returns(new IdentityOptions());
            var servicesMock = new Mock<IServiceProvider>();

            var userManager = new UserManager<IdentityUser>(
                userStore,
                optionsAccessorMock.Object,
                new PasswordHasher<IdentityUser>(),
                new List<IUserValidator<IdentityUser>> { new UserValidator<IdentityUser>() },
                new List<IPasswordValidator<IdentityUser>>(),
                new UpperInvariantLookupNormalizer(),
                new IdentityErrorDescriber(),
                servicesMock.Object,
                new NullLogger<UserManager<IdentityUser>>());

            var roleStore = new RoleStore<IdentityRole>(context);
            var roleManager = new RoleManager<IdentityRole>(
                roleStore,
                new List<IRoleValidator<IdentityRole>> { new RoleValidator<IdentityRole>() },
                new UpperInvariantLookupNormalizer(),
                new IdentityErrorDescriber(),
                new NullLogger<RoleManager<IdentityRole>>());

            return (userManager, roleManager);
        }

        private static TeacherService CreateService(ApplicationDbContext context)
        {
            var (userManager, roleManager) = CreateIdentityManagers(context);
            var uow = new UnitOfWork<ApplicationDbContext>(context);
            var repo = new TeacherRepository(context, uow);
            return new TeacherService(repo, userManager, roleManager);
        }

        #region Normal Test Cases (Kiểm thử giá trị thông thường)

        [Fact]
        public async Task GetAllAsync_WithKeywordAndFilters_ShouldReturnMatchingTeachers()
        {
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                context.Teachers.AddRange(
                    new Teacher
                    {
                        Code = "TC001",
                        Name = "Nguyen Van A",
                        Email = "a@test.com",
                        TextSearch = "TC001 Nguyen Van A a@test.com",
                        Status = (int)TeacherStatus.Active,
                        GradeLevel = GradeLevel.Foundation,
                        Gender = true
                    },
                    new Teacher
                    {
                        Code = "TC002",
                        Name = "Tran Van B",
                        Email = "b@test.com",
                        TextSearch = "TC002 Tran Van B b@test.com",
                        Status = (int)TeacherStatus.Inactive,
                        GradeLevel = GradeLevel.Ielts5_6,
                        Gender = false
                    });
                await context.SaveChangesAsync();
            }

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateService(context);
                var response = await service.GetAllAsync(new TeacherSearchDto
                {
                    Keyword = "Nguyen",
                    TeacherStatus = (int)TeacherStatus.Active,
                    GradeLevel = GradeLevel.Foundation,
                    Gender = true,
                    PageIndex = 1,
                    PageSize = 10
                });

                response.Success.Should().BeTrue();
                response.Data!.Items.Should().ContainSingle();
                response.Data.Items.First().Code.Should().Be("TC001");
            }
        }
        [Fact]
        public async Task CreateAsync_WithValidDto_ShouldCreateTeacher()
        {
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            using var context = new ApplicationDbContext(options, mockHttp.Object);
            var service = CreateService(context);

            var response = await service.CreateAsync(new TeacherSaveDto
            {
                Code = "TC001",
                Name = "Teacher One",
                Email = "teacher1@test.com",
                Phone = "0900000001",
                GradeLevel = GradeLevel.PreIelts
            });

            response.Success.Should().BeTrue();
            response.Data!.Code.Should().Be("TC001");
            response.Data.Status.Should().Be((int)TeacherStatus.Active);
            response.Data.GradeLevelName.Should().Be("Pre-IELTS");
        }
        [Fact]
        public async Task EditAsync_WhenEmailChanges_ShouldSyncIdentityUser()
        {
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int teacherId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, _) = CreateIdentityManagers(context);
                var teacher = new Teacher { Code = "TC001", Name = "Teacher One", Email = "old@test.com", Status = (int)TeacherStatus.Active };
                context.Teachers.Add(teacher);
                await context.SaveChangesAsync();
                teacherId = teacher.Id;

                await userManager.CreateAsync(new IdentityUser { UserName = "old", Email = "old@test.com", EmailConfirmed = true }, "123456");
            }

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var repo = new TeacherRepository(context, uow);
                var service = new TeacherService(repo, userManager, roleManager);

                var response = await service.EditAsync(new TeacherSaveDto
                {
                    Id = teacherId,
                    Code = "TC001",
                    Name = "Teacher One Updated",
                    Email = "new@test.com",
                    Status = (int)TeacherStatus.Active
                });

                response.Success.Should().BeTrue();
                (await userManager.FindByEmailAsync("old@test.com")).Should().BeNull();
                var newUser = await userManager.FindByEmailAsync("new@test.com");
                newUser.Should().NotBeNull();
                newUser!.UserName.Should().Be("new");
            }
        }
        [Fact]
        public async Task DeleteAsync_WhenTeacherExists_ShouldSoftDeleteTeacherAndDeleteIdentityUser()
        {
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int teacherId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, _) = CreateIdentityManagers(context);
                var teacher = new Teacher { Code = "TC001", Name = "Teacher One", Email = "delete@test.com" };
                context.Teachers.Add(teacher);
                await context.SaveChangesAsync();
                teacherId = teacher.Id;

                await userManager.CreateAsync(new IdentityUser { UserName = "delete", Email = "delete@test.com", EmailConfirmed = true }, "123456");
            }

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                var service = new TeacherService(new TeacherRepository(context, new UnitOfWork<ApplicationDbContext>(context)), userManager, roleManager);

                var response = await service.DeleteAsync(teacherId);

                response.Success.Should().BeTrue();
                response.Data.Should().BeTrue();
                (await context.Teachers.IgnoreQueryFilters().FirstAsync(t => t.Id == teacherId)).IsDeleted.Should().BeTrue();
                (await userManager.FindByEmailAsync("delete@test.com")).Should().BeNull();
            }
        }
        [Fact]
        public async Task DeactiveAsync_WhenTeacherExists_ShouldDeactivateAndLockIdentityUser()
        {
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int teacherId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, _) = CreateIdentityManagers(context);
                var teacher = new Teacher { Code = "TC001", Name = "Teacher One", Email = "deactive@test.com", Status = (int)TeacherStatus.Active };
                context.Teachers.Add(teacher);
                await context.SaveChangesAsync();
                teacherId = teacher.Id;

                await userManager.CreateAsync(new IdentityUser { UserName = "deactive", Email = "deactive@test.com", EmailConfirmed = true }, "123456");
            }

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                var service = new TeacherService(new TeacherRepository(context, new UnitOfWork<ApplicationDbContext>(context)), userManager, roleManager);

                var response = await service.DeactiveAsync(teacherId);

                response.Success.Should().BeTrue();
                var teacher = await context.Teachers.IgnoreQueryFilters().FirstAsync(t => t.Id == teacherId);
                teacher.IsDeleted.Should().BeTrue();

                var user = await userManager.FindByEmailAsync("deactive@test.com");
                (await userManager.IsLockedOutAsync(user!)).Should().BeTrue();
            }
        }
        [Fact]
        public async Task BulkProvisionAccountsAsync_ShouldCreateTeacherAccounts()
        {
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int teacherId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var teacher = new Teacher { Code = "TC001", Name = "Teacher One", Email = "bulk@test.com", Phone = "0900000000" };
                context.Teachers.Add(teacher);
                await context.SaveChangesAsync();
                teacherId = teacher.Id;
            }

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                var service = new TeacherService(new TeacherRepository(context, new UnitOfWork<ApplicationDbContext>(context)), userManager, roleManager);

                var response = await service.BulkProvisionAccountsAsync(new List<int> { teacherId });

                response.Success.Should().BeTrue();
                var user = await userManager.FindByEmailAsync("bulk@test.com");
                user.Should().NotBeNull();
                (await userManager.IsInRoleAsync(user!, "Teacher")).Should().BeTrue();
            }
        }
        [Fact]
        public async Task GetByIdAsync_WhenIdentityAccountExists_ShouldSetHasAccount()
        {
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            using var context = new ApplicationDbContext(options, mockHttp.Object);
            var teacher = new Teacher { Code = "TC001", Name = "Teacher One", Email = "account@test.com" };
            context.Teachers.Add(teacher);
            await context.SaveChangesAsync();
            var (userManager, roleManager) = CreateIdentityManagers(context);
            await userManager.CreateAsync(new IdentityUser { UserName = "account", Email = teacher.Email, EmailConfirmed = true }, "123456");
            var service = new TeacherService(
                new TeacherRepository(context, new UnitOfWork<ApplicationDbContext>(context)),
                userManager,
                roleManager);

            var response = await service.GetByIdAsync(teacher.Id);

            response.Success.Should().BeTrue();
            response.Data!.HasAccount.Should().BeTrue();
        }

        #endregion

        #region Boundary Test Cases (Kiểm thử giá trị biên)

        [Fact]
        public async Task ImportAsync_ShouldCreateValidRowsAndSkipInvalidRows()
        {
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            using var context = new ApplicationDbContext(options, mockHttp.Object);

            var response = await CreateService(context).ImportAsync(new List<TeacherSaveDto>
            {
                new() { Code = "TC001", Name = "Teacher One", Email = "one@test.com" },
                new() { Code = "", Name = "Invalid Teacher", Email = "invalid@test.com" },
                new() { Code = "TC002", Name = "Teacher Two", Email = "two@test.com" }
            });

            response.Success.Should().BeTrue();
            response.Data.Should().HaveCount(2);
            response.Data!.Select(x => x.Code).Should().BeEquivalentTo(new[] { "TC001", "TC002" });
            (await context.Teachers.CountAsync()).Should().Be(2);
        }

        #endregion

        #region Abnormal Test Cases (Kiểm thử giá trị bất thường / Lỗi)

        [Fact]
        public async Task CreateAsync_WithDuplicateCode_ShouldReturnBadRequest()
        {
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            using var context = new ApplicationDbContext(options, mockHttp.Object);
            context.Teachers.Add(new Teacher { Code = "DUP", Name = "Teacher One", Email = "one@test.com" });
            await context.SaveChangesAsync();

            var service = CreateService(context);
            var response = await service.CreateAsync(new TeacherSaveDto
            {
                Code = "DUP",
                Name = "Teacher Two",
                Email = "two@test.com"
            });

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
            response.Message.Should().Be("ERR_CODE_DUPLICATE");
        }
        [Fact]
        public async Task GetByIdAsync_WhenTeacherDoesNotExist_ShouldReturnNotFound()
        {
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            using var context = new ApplicationDbContext(options, mockHttp.Object);
            var service = CreateService(context);

            var response = await service.GetByIdAsync(9999);

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
            response.Message.Should().Be("ERR_TEACHER_NOT_FOUND");
        }
        [Fact]
        public async Task CreateAsync_WithDuplicateEmail_ShouldReturnBadRequest()
        {
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            using var context = new ApplicationDbContext(options, mockHttp.Object);
            context.Teachers.Add(new Teacher { Code = "TC001", Name = "Teacher One", Email = "same@test.com" });
            await context.SaveChangesAsync();

            var response = await CreateService(context).CreateAsync(new TeacherSaveDto
            {
                Code = "TC002",
                Name = "Teacher Two",
                Email = "same@test.com"
            });

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
            response.Message.Should().Be("ERR_EMAIL_DUPLICATE");
        }
        [Fact]
        public async Task BulkProvisionAccountsAsync_WithEmptySelection_ShouldReturnBadRequest()
        {
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            using var context = new ApplicationDbContext(options, mockHttp.Object);

            var response = await CreateService(context).BulkProvisionAccountsAsync(new List<int>());

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
            response.Message.Should().Be("ERR_NO_TEACHERS_SELECTED");
        }
        [Fact]
        public async Task BulkProvisionAccountsAsync_WhenTeacherHasNoEmail_ShouldReturnDetailedFailure()
        {
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            using var context = new ApplicationDbContext(options, mockHttp.Object);
            var teacher = new Teacher { Code = "TC001", Name = "Teacher One", Email = null };
            context.Teachers.Add(teacher);
            await context.SaveChangesAsync();

            var response = await CreateService(context).BulkProvisionAccountsAsync(new List<int> { teacher.Id });

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
            response.Message.Should().Contain("không có email");
        }
        [Fact]
        public async Task DeleteAndDeactivateAsync_WhenTeacherDoesNotExist_ShouldReturnNotFound()
        {
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            using var context = new ApplicationDbContext(options, mockHttp.Object);
            var service = CreateService(context);

            var deleteResponse = await service.DeleteAsync(9999);
            var deactivateResponse = await service.DeactiveAsync(9999);

            deleteResponse.Success.Should().BeFalse();
            deleteResponse.StatusCode.Should().Be(StatusCodes.Status404NotFound);
            deactivateResponse.Success.Should().BeFalse();
            deactivateResponse.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        }

        #endregion

    }
}
