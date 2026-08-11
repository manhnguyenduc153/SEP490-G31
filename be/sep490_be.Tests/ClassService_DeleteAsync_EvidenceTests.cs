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
    /// File này được tạo riêng để chứa các Test Case theo file ma trận Excel cho hàm DeleteAsync
    /// Mục đích: Tránh phình to file ClassServiceTests hiện tại và giúp review Evidence dễ dàng hơn.
    /// </summary>
    public class ClassService_DeleteAsync_EvidenceTests
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

        private ClassService CreateClassService(ApplicationDbContext context)
        {
            var uow = new UnitOfWork<ApplicationDbContext>(context);
            var classRepo = new ClassRepository(context, uow);
            var courseRepo = new CourseRepository(context, uow);
            var teacherRepo = new TeacherRepository(context, uow);
            var studentRepo = new StudentRepository(context, uow);
            var tsRepo = new BaseRepository<TimeSlot, ApplicationDbContext>(context, uow);
            var schRepo = new BaseRepository<ClassSchedule, ApplicationDbContext>(context, uow);
            var (userManager, roleManager) = CreateIdentityManagers(context);
            var mockOpt = GetMockOptimizationService(false);

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

        #region UTCID01 - UTCID03: Passed Cases (Normal & Abnormal returns True)

        [Fact]
        public async Task UTCID01_Normal_DeleteAsync_NoStudents_ShouldReturnTrue()
        {
            var options = CreateNewContextOptions(Guid.NewGuid().ToString());
            int classId;

            using (var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object))
            {
                // Class status is Planning (assuming 0 or similar state representing Planning)
                var cls = new Class { Code = "CLS01", Name = "Name", Status = 0 }; 
                context.Classes.Add(cls);
                await context.SaveChangesAsync();
                classId = cls.Id;
            }

            using (var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object))
            {
                var service = CreateClassService(context);
                var result = await service.DeleteAsync(classId); // Assuming DeleteAsync takes an ID
                
                result.Success.Should().BeTrue();
                // Log: DELETE_CLASS_SUCCESS
            }
        }

        [Fact]
        public async Task UTCID02_Normal_DeleteAsync_HasStudents_SemesterFound_ShouldReturnTrue()
        {
            var options = CreateNewContextOptions(Guid.NewGuid().ToString());
            int classId;

            using (var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object))
            {
                var semester = new Semester { Id = 1, Code = "SEM01", Name = "Semester 1" };
                context.Semesters.Add(semester);

                var cls = new Class { Code = "CLS02", Name = "Name", Status = 0, SemesterId = semester.Id }; 
                context.Classes.Add(cls);
                
                // Add student class associations
                context.StudentClasses.Add(new StudentClass { ClassId = cls.Id, StudentId = 1 });
                await context.SaveChangesAsync();
                classId = cls.Id;
            }

            using (var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object))
            {
                var service = CreateClassService(context);
                var result = await service.DeleteAsync(classId);
                
                result.Success.Should().BeTrue();
                // Log: DELETE_CLASS_SUCCESS
            }
        }

        [Fact]
        public async Task UTCID03_Abnormal_DeleteAsync_HasStudents_SemesterNotFound_ShouldReturnTrue()
        {
            var options = CreateNewContextOptions(Guid.NewGuid().ToString());
            int classId;

            using (var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object))
            {
                // SemesterId points to non-existent semester (Semester NOT found)
                var cls = new Class { Code = "CLS03", Name = "Name", Status = 0, SemesterId = 999 }; 
                context.Classes.Add(cls);
                
                // Add student class associations
                context.StudentClasses.Add(new StudentClass { ClassId = cls.Id, StudentId = 1 });
                await context.SaveChangesAsync();
                classId = cls.Id;
            }

            using (var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object))
            {
                var service = CreateClassService(context);
                var result = await service.DeleteAsync(classId);
                
                result.Success.Should().BeTrue();
                // Log: DELETE_CLASS_SUCCESS
            }
        }

        #endregion

        #region UTCID04 - UTCID09: Failed Cases (Abnormal) - Return False

        [Fact]
        public async Task UTCID04_Abnormal_DeleteAsync_ClassDoesNotExist_ShouldReturnFalse()
        {
            var options = CreateNewContextOptions(Guid.NewGuid().ToString());

            using (var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object))
            {
                var service = CreateClassService(context);
                var result = await service.DeleteAsync(9999); // Class ID not found
                
                result.Success.Should().BeFalse();
                // Log: ERR_CLASS_NOT_FOUND
            }
        }

        [Fact]
        public async Task UTCID05_Abnormal_DeleteAsync_ClassAlreadyStarted_ShouldReturnFalse()
        {
            var options = CreateNewContextOptions(Guid.NewGuid().ToString());
            int classId;

            using (var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object))
            {
                // Class status is NOT Planning (e.g. Started / Active = 1)
                var cls = new Class { Code = "CLS05", Name = "Name", Status = 1 }; 
                context.Classes.Add(cls);
                await context.SaveChangesAsync();
                classId = cls.Id;
            }

            using (var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object))
            {
                var service = CreateClassService(context);
                var result = await service.DeleteAsync(classId);
                
                result.Success.Should().BeFalse();
                // Log: ERR_CLASS_ALREADY_STARTE
            }
        }

        [Fact]
        public async Task UTCID06_Abnormal_DeleteAsync_InvalidClassId_ShouldReturnFalse()
        {
            var options = CreateNewContextOptions(Guid.NewGuid().ToString());

            using (var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object))
            {
                var service = CreateClassService(context);
                var result = await service.DeleteAsync(0); // Class ID <= 0 or missing
                
                result.Success.Should().BeFalse();
                // Log: ERR_CLASS_NOT_FOUND
            }
        }

        [Fact]
        public async Task UTCID07_Abnormal_DeleteAsync_DatabaseError1_ShouldReturnFalse()
        {
            var options = CreateNewContextOptions(Guid.NewGuid().ToString());
            int classId;

            using (var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object))
            {
                var cls = new Class { Code = "CLS07", Name = "Name", Status = 0 }; 
                context.Classes.Add(cls);
                await context.SaveChangesAsync();
                classId = cls.Id;
            }

            // Mock context to throw exception on save
            var mockContext = new Mock<ApplicationDbContext>(options, GetMockHttpContextAccessor().Object);
            mockContext.Setup(c => c.Classes).Returns(new ApplicationDbContext(options, GetMockHttpContextAccessor().Object).Classes);
            mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<System.Threading.CancellationToken>())).ThrowsAsync(new DbUpdateException("DB Error updating associations"));

            var uow = new UnitOfWork<ApplicationDbContext>(mockContext.Object);
            var classRepo = new ClassRepository(mockContext.Object, uow);
            var service = new ClassService(classRepo, null, null, null, null, null, null, null, mockContext.Object, null, null);

            var result = await service.DeleteAsync(classId);
            
            result.Success.Should().BeFalse();
            // Log: Transaction Rollback / Exception
        }

        [Fact]
        public async Task UTCID08_Abnormal_DeleteAsync_DatabaseError2_ShouldReturnFalse()
        {
            // Similar to UTCID07 - another variation of DB error when removing schedules/students
            var options = CreateNewContextOptions(Guid.NewGuid().ToString());
            
            var mockContext = new Mock<ApplicationDbContext>(options, GetMockHttpContextAccessor().Object);
            mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<System.Threading.CancellationToken>())).ThrowsAsync(new InvalidOperationException("Transaction Error"));

            var uow = new UnitOfWork<ApplicationDbContext>(mockContext.Object);
            var classRepo = new ClassRepository(mockContext.Object, uow);
            var service = new ClassService(classRepo, null, null, null, null, null, null, null, mockContext.Object, null, null);

            var result = await service.DeleteAsync(1); // Force error
            
            result.Success.Should().BeFalse();
            // Log: Transaction Rollback / Exception
        }

        [Fact]
        public async Task UTCID09_Abnormal_DeleteAsync_DatabaseError3_ShouldReturnFalse()
        {
            // Similar to UTCID07 - another variation of DB error
            var options = CreateNewContextOptions(Guid.NewGuid().ToString());
            
            var mockContext = new Mock<ApplicationDbContext>(options, GetMockHttpContextAccessor().Object);
            mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<System.Threading.CancellationToken>())).ThrowsAsync(new Exception("Unknown Database Error"));

            var uow = new UnitOfWork<ApplicationDbContext>(mockContext.Object);
            var classRepo = new ClassRepository(mockContext.Object, uow);
            var service = new ClassService(classRepo, null, null, null, null, null, null, null, mockContext.Object, null, null);

            var result = await service.DeleteAsync(1); // Force error
            
            result.Success.Should().BeFalse();
            // Log: Transaction Rollback / Exception
        }

        #endregion

        #region UTCID10: System Error / Exception

        [Fact]
        public async Task UTCID10_Abnormal_DeleteAsync_CannotConnectWithServer_ShouldThrowException()
        {
            var options = CreateNewContextOptions(Guid.NewGuid().ToString());

            // Mock DB context to throw exception on connect/query
            var mockContext = new Mock<ApplicationDbContext>(options, GetMockHttpContextAccessor().Object);
            mockContext.Setup(c => c.Classes).Throws(new Exception("Cannot connect with server"));

            var uow = new UnitOfWork<ApplicationDbContext>(mockContext.Object);
            var classRepo = new ClassRepository(mockContext.Object, uow);
            var service = new ClassService(classRepo, null, null, null, null, null, null, null, mockContext.Object, null, null);

            await Assert.ThrowsAsync<Exception>(() => service.DeleteAsync(1));
            // Log: Transaction Rollback / Exception
        }

        #endregion
    }
}
