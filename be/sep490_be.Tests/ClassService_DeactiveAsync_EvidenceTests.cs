using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using sep490_be.DTO;
using sep490_be.DTO.Class;
using sep490_be.Enums;
using sep490_be.Models;
using sep490_be.Repositories.Common;
using sep490_be.Repositories.Implementations;
using sep490_be.Services.Implementations;
using sep490_be.Services.Interfaces;

namespace sep490_be.Tests.Services
{
    /// <summary>
    /// File này được tạo dựa trên ảnh chụp mẫu code cho hàm DeactiveAsync
    /// Mục đích: Evidence cho case test duy nhất của DeactiveAsync.
    /// </summary>
    public class ClassService_DeactiveAsync_EvidenceTests
    {
        private DbContextOptions<ApplicationDbContext> CreateNewContextOptions()
        {
            return new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }

        private Mock<IHttpContextAccessor> GetMockHttpContextAccessor()
        {
            return new Mock<IHttpContextAccessor>();
        }

        private (UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager) CreateIdentityManagers(ApplicationDbContext context)
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

        private Mock<IScheduleOptimizationService> GetMockOptimizationService()
        {
            var mock = new Mock<IScheduleOptimizationService>();
            mock.Setup(x => x.CheckConflictAsync(It.IsAny<ClassSaveDto>()))
                .ReturnsAsync(ApiResponse<ConflictCheckResultDto>.Ok(new ConflictCheckResultDto { HasConflict = false }, "SUCCESS"));
            return mock;
        }

        [Fact]
        public async Task Normal_DeactiveAsync_ShouldDeactivateClass()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            var mockOpt = GetMockOptimizationService();
            int classId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var cls = new Class { Code = "CLS01", Name = "Math Class", Status = (int)ClassStatus.Active };
                context.Classes.Add(cls);
                await context.SaveChangesAsync();
                classId = cls.Id;
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var classRepo = new ClassRepository(context, uow);
                var courseRepo = new CourseRepository(context, uow);
                var teacherRepo = new TeacherRepository(context, uow);
                var studentRepo = new StudentRepository(context, uow);
                var tsRepo = new BaseRepository<TimeSlot, ApplicationDbContext>(context, uow);
                var schRepo = new BaseRepository<ClassSchedule, ApplicationDbContext>(context, uow);
                var (userManager, roleManager) = CreateIdentityManagers(context);

                var studentRegRepo = new BaseRepository<StudentRegistration, ApplicationDbContext>(context, uow);
                var studentClassRepo = new BaseRepository<StudentClass, ApplicationDbContext>(context, uow);
                var roomRepo = new BaseRepository<Room, ApplicationDbContext>(context, uow);
                var semesterRepo = new BaseRepository<Semester, ApplicationDbContext>(context, uow);
                var attendanceRepo = new BaseRepository<Attendance, ApplicationDbContext>(context, uow);
                var parentStudentLinkRepo = new BaseRepository<ParentStudentLink, ApplicationDbContext>(context, uow);

                var service = new ClassService(
                    classRepo, courseRepo, teacherRepo, studentRepo, tsRepo, schRepo, 
                    userManager, roleManager, studentRegRepo, studentClassRepo, roomRepo, 
                    semesterRepo, attendanceRepo, parentStudentLinkRepo, 
                    mockOpt.Object, Mock.Of<INotificationService>());

                var response = await service.DeactiveAsync(classId);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();

                var deactivated = await context.Classes.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == classId);
                deactivated!.IsDeleted.Should().BeTrue();
            }
        }
    }
}
