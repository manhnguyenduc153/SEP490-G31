using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using FluentAssertions;
using sep490_be.DTO.User;
using sep490_be.Models;
using sep490_be.Services.Implementations;
using Xunit;

namespace sep490_be.Tests.Services
{
    public class UserServiceTests
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

        private static UserService CreateService(UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager, ApplicationDbContext dbContext)
        {
            return new UserService(userManager, roleManager, dbContext);
        }

        #region Normal Test Cases (Kiểm thử giá trị thông thường)

        [Fact]
        public async Task Normal_GetAllAsync_WithKeywordAndRoleFilters_ShouldReturnMatchingUsers()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);

                await roleManager.CreateAsync(new IdentityRole { Name = "Teacher" });
                await roleManager.CreateAsync(new IdentityRole { Name = "Student" });

                var u1 = new IdentityUser { UserName = "teacher1", Email = "teacher1@test.com", PhoneNumber = "111222" };
                var u2 = new IdentityUser { UserName = "student1", Email = "student1@test.com", PhoneNumber = "333444" };

                await userManager.CreateAsync(u1);
                await userManager.CreateAsync(u2);

                await userManager.AddToRoleAsync(u1, "Teacher");
                await userManager.AddToRoleAsync(u2, "Student");
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                var service = CreateService(userManager, roleManager, context);

                // Test keyword filter
                var responseKeyword = await service.GetAllAsync(new UserSearchDto
                {
                    Keyword = "teacher",
                    PageIndex = 1,
                    PageSize = 10
                });

                // Test role filter
                var responseRole = await service.GetAllAsync(new UserSearchDto
                {
                    RoleName = "Student",
                    PageIndex = 1,
                    PageSize = 10
                });

                // Assert
                responseKeyword.Success.Should().BeTrue();
                responseKeyword.Data!.Items.Should().ContainSingle();
                responseKeyword.Data.Items.First().Username.Should().Be("teacher1");

                responseRole.Success.Should().BeTrue();
                responseRole.Data!.Items.Should().ContainSingle();
                responseRole.Data.Items.First().Username.Should().Be("student1");
            }
        }

        [Fact]
        public async Task Normal_GetByIdAsync_WhenUserExists_ShouldReturnUser()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            string userId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                var user = new IdentityUser { UserName = "testuser", Email = "test@test.com" };
                await userManager.CreateAsync(user);
                userId = user.Id;
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                var service = CreateService(userManager, roleManager, context);
                var response = await service.GetByIdAsync(userId);

                // Assert
                response.Success.Should().BeTrue();
                response.Data.Should().NotBeNull();
                response.Data!.Username.Should().Be("testuser");
                response.Data.Email.Should().Be("test@test.com");
            }
        }

        [Fact]
        public async Task Normal_CreateAsync_WithValidInputs_ShouldCreateUserAndAssignRole()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (_, roleManager) = CreateIdentityManagers(context);
                await roleManager.CreateAsync(new IdentityRole { Name = "Teacher" });
            }

            var dto = new UserCreateDto
            {
                Username = "newteacher",
                Email = "newteacher@test.com",
                Phone = "123456789",
                RoleName = "Teacher"
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                var service = CreateService(userManager, roleManager, context);
                var response = await service.CreateAsync(dto);

                // Assert
                response.Success.Should().BeTrue();
                response.Data.Should().NotBeNull();
                response.Data!.Username.Should().Be("newteacher");
                response.Data.Roles.Should().Contain("Teacher");

                var createdUser = await userManager.FindByNameAsync("newteacher");
                createdUser.Should().NotBeNull();
                (await userManager.IsInRoleAsync(createdUser!, "Teacher")).Should().BeTrue();
            }
        }

        [Fact]
        public async Task Normal_EditAsync_WithValidInputs_ShouldUpdateUserAndRole()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            string userId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                await roleManager.CreateAsync(new IdentityRole { Name = "Teacher" });
                await roleManager.CreateAsync(new IdentityRole { Name = "Student" });

                var user = new IdentityUser { UserName = "edituser", Email = "old@test.com", PhoneNumber = "111" };
                await userManager.CreateAsync(user);
                await userManager.AddToRoleAsync(user, "Teacher");
                userId = user.Id;
            }

            var dto = new UserUpdateDto
            {
                Id = userId,
                Email = "new@test.com",
                Phone = "222",
                RoleName = "Student"
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                var service = CreateService(userManager, roleManager, context);
                var response = await service.EditAsync(dto);

                // Assert
                response.Success.Should().BeTrue();
                response.Data!.Email.Should().Be("new@test.com");
                response.Data.Phone.Should().Be("222");
                response.Data.Roles.Should().Contain("Student");

                var updatedUser = await userManager.FindByIdAsync(userId);
                updatedUser!.Email.Should().Be("new@test.com");
                (await userManager.IsInRoleAsync(updatedUser, "Student")).Should().BeTrue();
                (await userManager.IsInRoleAsync(updatedUser, "Teacher")).Should().BeFalse();
            }
        }

        [Fact]
        public async Task Normal_DeleteAsync_WhenUserExists_ShouldDeleteUser()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            string userId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, _) = CreateIdentityManagers(context);
                var user = new IdentityUser { UserName = "deleteuser", Email = "delete@test.com" };
                await userManager.CreateAsync(user);
                userId = user.Id;
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                var service = CreateService(userManager, roleManager, context);
                var response = await service.DeleteAsync(userId);

                // Assert
                response.Success.Should().BeTrue();
                response.Data.Should().BeTrue();

                var userExists = await userManager.FindByIdAsync(userId);
                userExists.Should().BeNull();
            }
        }

        [Fact]
        public async Task Normal_DeactiveAsync_WhenUserExists_ShouldToggleLockout()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            string userId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, _) = CreateIdentityManagers(context);
                var user = new IdentityUser { UserName = "deactiveuser", Email = "deactive@test.com" };
                await userManager.CreateAsync(user);
                userId = user.Id;
            }

            // Act: Deactivate (Lock)
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                var service = CreateService(userManager, roleManager, context);
                var responseDeactive = await service.DeactiveAsync(userId);

                // Assert deactivation
                responseDeactive.Success.Should().BeTrue();
                var lockedUser = await userManager.FindByIdAsync(userId);
                (await userManager.IsLockedOutAsync(lockedUser!)).Should().BeTrue();

                // Act: Reactivate (Unlock)
                var responseReactive = await service.DeactiveAsync(userId);

                // Assert reactivation
                responseReactive.Success.Should().BeTrue();
                var unlockedUser = await userManager.FindByIdAsync(userId);
                (await userManager.IsLockedOutAsync(unlockedUser!)).Should().BeFalse();
            }
        }

        #endregion

        #region Abnormal Test Cases (Kiểm thử giá trị bất thường / Lỗi)

        [Fact]
        public async Task Abnormal_GetByIdAsync_WhenUserDoesNotExist_ShouldReturnNotFound()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                var service = CreateService(userManager, roleManager, context);
                var response = await service.GetByIdAsync("invalid-id");

                // Assert
                response.Success.Should().BeFalse();
                response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
                response.Message.Should().Be("ERR_USER_NOT_FOUND");
            }
        }

        [Fact]
        public async Task Abnormal_CreateAsync_WithEmptyUsername_ShouldReturnBadRequest()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            var dto = new UserCreateDto { Username = "", Email = "test@test.com", RoleName = "Teacher" };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                var service = CreateService(userManager, roleManager, context);
                var response = await service.CreateAsync(dto);

                // Assert
                response.Success.Should().BeFalse();
                response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
                response.Message.Should().Be("ERR_USERNAME_EMPTY");
            }
        }

        [Fact]
        public async Task Abnormal_CreateAsync_WithDuplicateUsername_ShouldReturnBadRequest()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, _) = CreateIdentityManagers(context);
                await userManager.CreateAsync(new IdentityUser { UserName = "existinguser", Email = "ex@test.com" });
            }

            var dto = new UserCreateDto { Username = "existinguser", Email = "new@test.com", RoleName = "Teacher" };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                var service = CreateService(userManager, roleManager, context);
                var response = await service.CreateAsync(dto);

                // Assert
                response.Success.Should().BeFalse();
                response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
                response.Message.Should().Be("ERR_USERNAME_DUPLICATE");
            }
        }

        [Fact]
        public async Task Abnormal_CreateAsync_WithDuplicateEmail_ShouldReturnBadRequest()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, _) = CreateIdentityManagers(context);
                await userManager.CreateAsync(new IdentityUser { UserName = "user1", Email = "dup@test.com" });
            }

            var dto = new UserCreateDto { Username = "user2", Email = "dup@test.com", RoleName = "Teacher" };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                var service = CreateService(userManager, roleManager, context);
                var response = await service.CreateAsync(dto);

                // Assert
                response.Success.Should().BeFalse();
                response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
                response.Message.Should().Be("ERR_EMAIL_DUPLICATE");
            }
        }

        [Fact]
        public async Task Abnormal_CreateAsync_AssigningAdminRole_ShouldReturnBadRequest()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            var dto = new UserCreateDto { Username = "adminuser", Email = "admin@test.com", RoleName = "Admin" };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                var service = CreateService(userManager, roleManager, context);
                var response = await service.CreateAsync(dto);

                // Assert
                response.Success.Should().BeFalse();
                response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
                response.Message.Should().Be("ERR_CANNOT_ASSIGN_ADMIN_ROLE");
            }
        }

        [Fact]
        public async Task Abnormal_EditAsync_WithDuplicateEmail_ShouldReturnBadRequest()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            string editUserId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                await roleManager.CreateAsync(new IdentityRole { Name = "Teacher" });

                var u1 = new IdentityUser { UserName = "user1", Email = "dup@test.com" };
                var u2 = new IdentityUser { UserName = "user2", Email = "other@test.com" };

                await userManager.CreateAsync(u1);
                await userManager.CreateAsync(u2);
                editUserId = u2.Id;
            }

            var dto = new UserUpdateDto
            {
                Id = editUserId,
                Email = "dup@test.com", // Duplicate of user1's email
                RoleName = "Teacher"
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                var service = CreateService(userManager, roleManager, context);
                var response = await service.EditAsync(dto);

                // Assert
                response.Success.Should().BeFalse();
                response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
                response.Message.Should().Be("ERR_EMAIL_DUPLICATE");
            }
        }

        #endregion
    }
}
