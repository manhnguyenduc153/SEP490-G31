using System;
using System.Collections.Generic;
using System.Linq;
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
    public class ClassService_GetStudentSchedulesAsync_EvidenceTests
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
        public async Task Normal_GetStudentSchedulesAsync_ShouldReturnSchedules()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            var mockOpt = GetMockOptimizationService();
            string testUsername = "student_test";
            string testEmail = "student_test@test.com";

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                // Create User
                var user = new IdentityUser { UserName = testUsername, Email = testEmail };
                context.Users.Add(user);
                
                // Create Student
                var student = new Student { Code = "HS01", Name = "Student Test", Email = testEmail, Status = 1 };
                context.Students.Add(student);

                // Create Teacher
                var teacher = new Teacher { Code = "GV01", Name = "Mr. Teacher", Status = 1 };
                context.Teachers.Add(teacher);

                // Create Class and Schedules
                var cls = new Class { Code = "CLS01", Name = "Math Class", Status = (int)ClassStatus.Active };
                context.Classes.Add(cls);
                
                // Enroll student into class
                var studentClass = new StudentClass 
                { 
                    StudentId = student.Id, 
                    ClassId = cls.Id, 
                    Status = (int)StudentClassStatus.Enrolled 
                };
                context.StudentClasses.Add(studentClass);
                
                var schedule = new ClassSchedule 
                { 
                    ClassId = cls.Id,
                    TeacherId = teacher.Id,
                    ScheduleDate = DateTime.UtcNow.Date,
                    LessonNo = 1,
                    Status = 1,
                    Class = cls
                };
                context.ClassSchedules.Add(schedule);
                
                await context.SaveChangesAsync();
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

                var response = await service.GetStudentSchedulesAsync(testUsername);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data.Should().NotBeNull();
                response.Data.Should().HaveCount(1);
                response.Data.First().ClassCode.Should().Be("CLS01");
            }
        }

        [Fact]
        public async Task Abnormal_GetStudentSchedulesAsync_UserNotFound_ShouldReturn404()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            var mockOpt = GetMockOptimizationService();

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

                var response = await service.GetStudentSchedulesAsync("unknown_student");

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.Message.Should().Be("ERR_USER_NOT_FOUND");
            }
        }

        [Fact]
        public async Task Abnormal_GetStudentSchedulesAsync_StudentNotFound_ShouldReturn404()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            var mockOpt = GetMockOptimizationService();
            string testUsername = "user_no_student";
            string testEmail = "user_no_student@test.com";

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                // Chỉ tạo User nhưng không có record tương ứng trong Students table
                var user = new IdentityUser { UserName = testUsername, Email = testEmail };
                context.Users.Add(user);
                await context.SaveChangesAsync();
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

                var response = await service.GetStudentSchedulesAsync(testUsername);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.Message.Should().Be("ERR_STUDENT_NOT_FOUND");
            }
        }
    }
}
