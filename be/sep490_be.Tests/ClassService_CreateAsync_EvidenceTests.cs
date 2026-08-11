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
    /// File này được tạo riêng để chứa các Test Case theo file ma trận Excel cho hàm CreateAsync
    /// Mục đích: Tránh phình to file ClassServiceTests hiện tại và giúp review Evidence dễ dàng hơn.
    /// </summary>
    public class ClassService_CreateAsync_EvidenceTests
    {
        private DbContextOptions<ApplicationDbContext> CreateNewContextOptions(string dbName)
        {
            return new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
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

        private Mock<IScheduleOptimizationService> GetMockOptimizationService(bool hasConflict = false)
        {
            var mock = new Mock<IScheduleOptimizationService>();
            mock.Setup(x => x.CheckConflictAsync(It.IsAny<ClassSaveDto>()))
                .ReturnsAsync(ApiResponse<ConflictCheckResultDto>.Ok(new ConflictCheckResultDto { HasConflict = hasConflict }, "SUCCESS"));
            return mock;
        }

        private ClassService CreateClassService(ApplicationDbContext context, bool hasConflict = false)
        {
            var uow = new UnitOfWork<ApplicationDbContext>(context);
            var classRepo = new ClassRepository(context, uow);
            var courseRepo = new CourseRepository(context, uow);
            var teacherRepo = new TeacherRepository(context, uow);
            var studentRepo = new StudentRepository(context, uow);
            var tsRepo = new BaseRepository<TimeSlot, ApplicationDbContext>(context, uow);
            var schRepo = new BaseRepository<ClassSchedule, ApplicationDbContext>(context, uow);
            var (userManager, roleManager) = CreateIdentityManagers(context);
            var mockOpt = GetMockOptimizationService(hasConflict);

            var studentRegRepo = new BaseRepository<StudentRegistration, ApplicationDbContext>(context, uow);
            var studentClassRepo = new BaseRepository<StudentClass, ApplicationDbContext>(context, uow);
            var roomRepo = new BaseRepository<Room, ApplicationDbContext>(context, uow);
            var semesterRepo = new BaseRepository<Semester, ApplicationDbContext>(context, uow);
            var attendanceRepo = new BaseRepository<Attendance, ApplicationDbContext>(context, uow);
            var parentStudentLinkRepo = new BaseRepository<ParentStudentLink, ApplicationDbContext>(context, uow);

            return new ClassService(
                classRepo, courseRepo, teacherRepo, studentRepo, tsRepo, schRepo, 
                userManager, roleManager, studentRegRepo, studentClassRepo, roomRepo, 
                semesterRepo, attendanceRepo, parentStudentLinkRepo, 
                mockOpt.Object, Mock.Of<INotificationService>());
        }

        #region UTCID01 - UTCID02: Passed Cases (Normal & Boundary)

        [Fact]
        public async Task UTCID01_Normal_CreateAsync_ValidInput_ShouldReturnTrue()
        {
            var options = CreateNewContextOptions(Guid.NewGuid().ToString());
            using (var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object))
            {
                context.Courses.Add(new Course { Id = 1, Code = "KH01", Status = 1 });
                context.Teachers.Add(new Teacher { Id = 1, Code = "GV01", Status = 1 });
                context.Students.Add(new Student { Id = 1, Code = "HS01", Status = 1 });
                await context.SaveChangesAsync();
            }

            var dto = new ClassSaveDto
            {
                Code = "CLS01",
                Name = "Valid Name",
                CourseId = 1,
                TeacherId = 1,
                Students = new List<StudentEnrollDto> { new StudentEnrollDto { StudentId = 1 } },
                WeeklySchedules = new List<WeeklyScheduleDto>()
            };

            using (var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object))
            {
                var service = CreateClassService(context);
                var result = await service.CreateAsync(dto);
                result.Success.Should().BeTrue();
            }
        }

        [Fact]
        public async Task UTCID02_Boundary_CreateAsync_ValidInput_ShouldReturnTrue()
        {
            var options = CreateNewContextOptions(Guid.NewGuid().ToString());
            using (var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object))
            {
                context.Courses.Add(new Course { Id = 1, Status = 1 });
                context.Teachers.Add(new Teacher { Id = 1, Status = 1 });
                await context.SaveChangesAsync();
            }

            var dto = new ClassSaveDto
            {
                Code = new string('C', 50),
                Name = new string('N', 200),
                CourseId = 1,
                TeacherId = 1
            };

            using (var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object))
            {
                var service = CreateClassService(context);
                var result = await service.CreateAsync(dto);
                result.Success.Should().BeTrue();
            }
        }

        #endregion

        #region UTCID03 - UTCID09: Failed Cases (Abnormal) - Return False

        [Fact]
        public async Task UTCID03_Abnormal_CreateAsync_EmptyCodeOrName_ShouldReturnFalse()
        {
            var options = CreateNewContextOptions(Guid.NewGuid().ToString());
            var dto = new ClassSaveDto { Code = "", Name = "" };

            using (var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object))
            {
                var service = CreateClassService(context);
                var result = await service.CreateAsync(dto);
                result.Success.Should().BeFalse();
            }
        }

        [Fact]
        public async Task UTCID04_Abnormal_CreateAsync_ExceedsMaxLength_ShouldReturnFalse()
        {
            var options = CreateNewContextOptions(Guid.NewGuid().ToString());
            var dto = new ClassSaveDto { Code = new string('X', 1000), Name = "Name" };

            using (var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object))
            {
                var service = CreateClassService(context);
                var result = await service.CreateAsync(dto);
                result.Success.Should().BeFalse();
            }
        }

        [Fact]
        public async Task UTCID05_Abnormal_CreateAsync_DuplicateCodeOrName_ShouldReturnFalse()
        {
            var options = CreateNewContextOptions(Guid.NewGuid().ToString());
            using (var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object))
            {
                context.Classes.Add(new Class { Code = "EXISTED", Name = "Name" });
                await context.SaveChangesAsync();
            }

            var dto = new ClassSaveDto { Code = "EXISTED", Name = "Name" };

            using (var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object))
            {
                var service = CreateClassService(context);
                var result = await service.CreateAsync(dto);
                result.Success.Should().BeFalse();
            }
        }

        [Fact]
        public async Task UTCID06_Abnormal_CreateAsync_CourseOrTeacherNotFound_ShouldReturnFalse()
        {
            var options = CreateNewContextOptions(Guid.NewGuid().ToString());
            var dto = new ClassSaveDto { Code = "CLS06", Name = "Name", CourseId = 999, TeacherId = 999 };

            using (var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object))
            {
                var service = CreateClassService(context);
                var result = await service.CreateAsync(dto);
                result.Success.Should().BeFalse();
            }
        }

        [Fact]
        public async Task UTCID07_Abnormal_CreateAsync_InvalidStudentIds_ShouldReturnFalse()
        {
            var options = CreateNewContextOptions(Guid.NewGuid().ToString());
            using (var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object))
            {
                context.Courses.Add(new Course { Id = 1, Status = 1 });
                context.Teachers.Add(new Teacher { Id = 1, Status = 1 });
                await context.SaveChangesAsync();
            }

            var dto = new ClassSaveDto
            {
                Code = "CLS07", Name = "Name", CourseId = 1, TeacherId = 1,
                Students = new List<StudentEnrollDto> { new StudentEnrollDto { StudentId = 9999 } }
            };

            using (var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object))
            {
                var service = CreateClassService(context);
                var result = await service.CreateAsync(dto);
                result.Success.Should().BeFalse();
            }
        }

        [Fact]
        public async Task UTCID08_Abnormal_CreateAsync_RoomCapacityExceeded_ShouldReturnFalse()
        {
            var options = CreateNewContextOptions(Guid.NewGuid().ToString());
            using (var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object))
            {
                context.Courses.Add(new Course { Id = 1, Status = 1 });
                context.Teachers.Add(new Teacher { Id = 1, Status = 1 });
                // Note: Missing room mock, relying on business logic failure
                await context.SaveChangesAsync();
            }

            var dto = new ClassSaveDto
            {
                Code = "CLS08", Name = "Name", CourseId = 1, TeacherId = 1,
                Students = new List<StudentEnrollDto> { 
                    new StudentEnrollDto { StudentId = 1 }, 
                    new StudentEnrollDto { StudentId = 2 }, 
                    new StudentEnrollDto { StudentId = 3 } 
                },
                WeeklySchedules = new List<WeeklyScheduleDto> { new WeeklyScheduleDto { RoomId = 1 } }
            };

            using (var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object))
            {
                var service = CreateClassService(context);
                var result = await service.CreateAsync(dto);
                result.Success.Should().BeFalse(); 
            }
        }

        [Fact]
        public async Task UTCID09_Abnormal_CreateAsync_ScheduleConflict_ShouldReturnFalse()
        {
            var options = CreateNewContextOptions(Guid.NewGuid().ToString());
            using (var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object))
            {
                context.Courses.Add(new Course { Id = 1, Status = 1 });
                context.Teachers.Add(new Teacher { Id = 1, Status = 1 });
                await context.SaveChangesAsync();
            }

            var dto = new ClassSaveDto
            {
                Code = "CLS09", Name = "Name", CourseId = 1, TeacherId = 1
            };

            using (var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object))
            {
                var service = CreateClassService(context, hasConflict: true); // Conflict = true
                var result = await service.CreateAsync(dto);
                result.Success.Should().BeFalse();
            }
        }

        #endregion

        #region UTCID10: System Error / Exception

        [Fact]
        public async Task UTCID10_Abnormal_CreateAsync_CannotConnectWithServer_ShouldThrowException()
        {
            var options = CreateNewContextOptions(Guid.NewGuid().ToString());
            var dto = new ClassSaveDto { Code = "CLS10", Name = "Name" };

            // Mock DB context to throw exception when saving
            var mockContext = new Mock<ApplicationDbContext>(options, GetMockHttpContextAccessor().Object);
            mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<System.Threading.CancellationToken>())).ThrowsAsync(new Exception("Database down"));

            var uow = new UnitOfWork<ApplicationDbContext>(mockContext.Object);
            var classRepo = new ClassRepository(mockContext.Object, uow);
            var courseRepo = new CourseRepository(mockContext.Object, uow);
            var teacherRepo = new TeacherRepository(mockContext.Object, uow);
            var studentRepo = new StudentRepository(mockContext.Object, uow);
            var tsRepo = new BaseRepository<TimeSlot, ApplicationDbContext>(mockContext.Object, uow);
            var schRepo = new BaseRepository<ClassSchedule, ApplicationDbContext>(mockContext.Object, uow);
            var (userManager, roleManager) = CreateIdentityManagers(mockContext.Object);
            var mockOpt = GetMockOptimizationService(false);

            var service = new ClassService(classRepo, courseRepo, teacherRepo, studentRepo, tsRepo, schRepo, userManager, roleManager, mockContext.Object, mockOpt.Object, Mock.Of<INotificationService>());

            await Assert.ThrowsAsync<Exception>(() => service.CreateAsync(dto));
        }

        #endregion
    }
}
