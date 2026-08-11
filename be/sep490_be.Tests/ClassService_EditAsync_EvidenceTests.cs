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
    /// File này được tạo riêng để chứa các Test Case theo file ma trận Excel cho hàm EditAsync
    /// Mục đích: Tránh phình to file ClassServiceTests hiện tại và giúp review Evidence dễ dàng hơn.
    /// </summary>
    public class ClassService_EditAsync_EvidenceTests
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
        public async Task UTCID01_Normal_EditAsync_ValidInput_ShouldReturnTrue()
        {
            var options = CreateNewContextOptions(Guid.NewGuid().ToString());
            int classId;

            using (var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object))
            {
                context.Courses.Add(new Course { Id = 1, Code = "KH01", Status = 1 });
                context.Teachers.Add(new Teacher { Id = 1, Code = "GV01", Status = 1 });
                context.Students.Add(new Student { Id = 1, Code = "HS01", Status = 1 });
                
                var cls = new Class { Code = "CLS01", Name = "Old Name", Status = (int)ClassStatus.Active };
                context.Classes.Add(cls);
                await context.SaveChangesAsync();
                classId = cls.Id;
            }

            var dto = new ClassSaveDto
            {
                Id = classId,
                Code = "CLS01_UPDATED",
                Name = "Valid Name",
                CourseId = 1,
                TeacherId = 1,
                Students = new List<StudentEnrollDto> { new StudentEnrollDto { StudentId = 1 } }
            };

            using (var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object))
            {
                var service = CreateClassService(context);
                var result = await service.EditAsync(dto);
                result.Success.Should().BeTrue();
            }
        }

        [Fact]
        public async Task UTCID02_Boundary_EditAsync_ValidInput_ShouldReturnTrue()
        {
            var options = CreateNewContextOptions(Guid.NewGuid().ToString());
            int classId;

            using (var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object))
            {
                context.Courses.Add(new Course { Id = 1, Status = 1 });
                context.Teachers.Add(new Teacher { Id = 1, Status = 1 });
                var cls = new Class { Code = "CLS02", Name = "Old Name", Status = (int)ClassStatus.Active };
                context.Classes.Add(cls);
                await context.SaveChangesAsync();
                classId = cls.Id;
            }

            var dto = new ClassSaveDto
            {
                Id = classId,
                Code = new string('C', 50), // Max Length Boundary
                Name = new string('N', 200), // Max Length Boundary
                CourseId = 1,
                TeacherId = 1
            };

            using (var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object))
            {
                var service = CreateClassService(context);
                var result = await service.EditAsync(dto);
                result.Success.Should().BeTrue();
            }
        }

        #endregion

        #region UTCID03 - UTCID09: Failed Cases (Abnormal) - Return False

        [Fact]
        public async Task UTCID03_Abnormal_EditAsync_ClassDoesNotExist_ShouldReturnFalse()
        {
            var options = CreateNewContextOptions(Guid.NewGuid().ToString());
            
            var dto = new ClassSaveDto 
            { 
                Id = 9999, // ID Not Found
                Code = "CLS03", 
                Name = "Name" 
            };

            using (var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object))
            {
                var service = CreateClassService(context);
                var result = await service.EditAsync(dto);
                result.Success.Should().BeFalse();
                // Log: ERR_CLASS_NOT_FOUND
            }
        }

        [Fact]
        public async Task UTCID04_Abnormal_EditAsync_ClassIsSoftDeleted_ShouldReturnFalse()
        {
            var options = CreateNewContextOptions(Guid.NewGuid().ToString());
            int classId;

            using (var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object))
            {
                // Class exists but IsDeleted / Inactive
                var cls = new Class { Code = "CLS04", Name = "Old Name", Status = (int)ClassStatus.Inactive /* Or IsDeleted = true */ };
                context.Classes.Add(cls);
                await context.SaveChangesAsync();
                classId = cls.Id;
            }

            var dto = new ClassSaveDto 
            { 
                Id = classId,
                Code = "CLS04", 
                Name = "Name" 
            };

            using (var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object))
            {
                var service = CreateClassService(context);
                var result = await service.EditAsync(dto);
                result.Success.Should().BeFalse();
                // Log: ERR_CLASS_NOT_FOUND
            }
        }

        [Fact]
        public async Task UTCID05_Abnormal_EditAsync_CodeOrNameEmpty_ShouldReturnFalse()
        {
            var options = CreateNewContextOptions(Guid.NewGuid().ToString());
            int classId;

            using (var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object))
            {
                var cls = new Class { Code = "CLS05", Name = "Old Name", Status = (int)ClassStatus.Active };
                context.Classes.Add(cls);
                await context.SaveChangesAsync();
                classId = cls.Id;
            }

            var dto = new ClassSaveDto 
            { 
                Id = classId,
                Code = "", // Missing/Empty code
                Name = ""  
            };

            using (var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object))
            {
                var service = CreateClassService(context);
                var result = await service.EditAsync(dto);
                result.Success.Should().BeFalse();
                // Log: ERR_DUPLICATE / ERR_EMPTY / ERR_INVALID
            }
        }

        [Fact]
        public async Task UTCID06_Abnormal_EditAsync_CodeOrNameExistsInAnotherClass_ShouldReturnFalse()
        {
            var options = CreateNewContextOptions(Guid.NewGuid().ToString());
            int classId;

            using (var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object))
            {
                var cls = new Class { Code = "CLS06", Name = "Old Name", Status = (int)ClassStatus.Active };
                var otherCls = new Class { Code = "EXISTED_CODE", Name = "Other Name", Status = (int)ClassStatus.Active };
                
                context.Classes.Add(cls);
                context.Classes.Add(otherCls);
                await context.SaveChangesAsync();
                
                classId = cls.Id;
            }

            var dto = new ClassSaveDto 
            { 
                Id = classId,
                Code = "EXISTED_CODE", // Name or Code exists in ANOTHER class
                Name = "New Name" 
            };

            using (var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object))
            {
                var service = CreateClassService(context);
                var result = await service.EditAsync(dto);
                result.Success.Should().BeFalse();
                // Log: ERR_DUPLICATE / ERR_EMPTY / ERR_INVALID
            }
        }

        [Fact]
        public async Task UTCID07_Abnormal_EditAsync_CourseOrTeacherNotFound_ShouldReturnFalse()
        {
            var options = CreateNewContextOptions(Guid.NewGuid().ToString());
            int classId;

            using (var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object))
            {
                var cls = new Class { Code = "CLS07", Name = "Old Name", Status = (int)ClassStatus.Active };
                context.Classes.Add(cls);
                await context.SaveChangesAsync();
                classId = cls.Id;
            }

            var dto = new ClassSaveDto 
            { 
                Id = classId,
                Code = "CLS07_UPDATED", 
                Name = "Name", 
                CourseId = 999, // CourseId not found
                TeacherId = 999 // TeacherId not found
            };

            using (var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object))
            {
                var service = CreateClassService(context);
                var result = await service.EditAsync(dto);
                result.Success.Should().BeFalse();
                // Log: ERR_COURSE_NOT_FOUND / ERR_TEACHER / STUDENT
            }
        }

        [Fact]
        public async Task UTCID08_Abnormal_EditAsync_RoomCapacityExceeded_ShouldReturnFalse()
        {
            var options = CreateNewContextOptions(Guid.NewGuid().ToString());
            int classId;

            using (var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object))
            {
                context.Courses.Add(new Course { Id = 1, Status = 1 });
                context.Teachers.Add(new Teacher { Id = 1, Status = 1 });
                
                var cls = new Class { Code = "CLS08", Name = "Old Name", Status = (int)ClassStatus.Active };
                context.Classes.Add(cls);
                await context.SaveChangesAsync();
                classId = cls.Id;
            }

            var dto = new ClassSaveDto
            {
                Id = classId,
                Code = "CLS08_UPDATED", 
                Name = "Name", 
                CourseId = 1, 
                TeacherId = 1,
                Students = new List<StudentEnrollDto> { 
                    new StudentEnrollDto { StudentId = 1 }, 
                    new StudentEnrollDto { StudentId = 2 }, 
                    new StudentEnrollDto { StudentId = 3 } 
                },
                WeeklySchedules = new List<WeeklyScheduleDto> { new WeeklyScheduleDto { RoomId = 1 } } // Assuming Room 1 is too small
            };

            using (var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object))
            {
                var service = CreateClassService(context);
                var result = await service.EditAsync(dto);
                result.Success.Should().BeFalse(); 
                // Log: ERR_ROOM_CAPACITY_EXCEEDED
            }
        }

        [Fact]
        public async Task UTCID09_Abnormal_EditAsync_ScheduleConflict_ShouldReturnFalse()
        {
            var options = CreateNewContextOptions(Guid.NewGuid().ToString());
            int classId;

            using (var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object))
            {
                context.Courses.Add(new Course { Id = 1, Status = 1 });
                context.Teachers.Add(new Teacher { Id = 1, Status = 1 });
                
                var cls = new Class { Code = "CLS09", Name = "Old Name", Status = (int)ClassStatus.Active };
                context.Classes.Add(cls);
                await context.SaveChangesAsync();
                classId = cls.Id;
            }

            var dto = new ClassSaveDto
            {
                Id = classId,
                Code = "CLS09_UPDATED", 
                Name = "Name", 
                CourseId = 1, 
                TeacherId = 1
            };

            using (var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object))
            {
                var service = CreateClassService(context, hasConflict: true); // Force conflict true
                var result = await service.EditAsync(dto);
                result.Success.Should().BeFalse();
                // Log: ERR_SCHEDULE_CONFLICT
            }
        }

        #endregion

        #region UTCID10: System Error / Exception

        [Fact]
        public async Task UTCID10_Abnormal_EditAsync_CannotConnectWithServer_ShouldThrowException()
        {
            var options = CreateNewContextOptions(Guid.NewGuid().ToString());
            var dto = new ClassSaveDto { Id = 1, Code = "CLS10", Name = "Name" };

            // Mock DB context to throw exception
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

            await Assert.ThrowsAsync<Exception>(() => service.EditAsync(dto));
        }

        #endregion
    }
}
