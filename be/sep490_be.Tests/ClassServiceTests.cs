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
    /// <summary>
    /// Unit test suite for ClassService.
    /// Code Module: ClassService
    /// </summary>
    public class ClassServiceTests
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

        #region Normal Test Cases (Kiểm thử giá trị thông thường)

        [Fact]
        public async Task Normal_GetAllAsync_ShouldReturnFilteredResults()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            var mockOpt = GetMockOptimizationService();

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var course = new Course { Code = "CRS01", Name = "Math Course", Status = 1 };
                var teacher = new Teacher { Code = "TCH01", Name = "Mr. Test", Email = "mrtest@test.com", Status = 1 };
                context.Courses.Add(course);
                context.Teachers.Add(teacher);
                await context.SaveChangesAsync();

                var class1 = new Class { Code = "CLS01", Name = "Grade 10 Math", CourseId = course.Id, TeacherId = teacher.Id, TextSearch = "CLS01 Grade 10 Math", Status = (int)ClassStatus.Active };
                var class2 = new Class { Code = "CLS02", Name = "Grade 11 History", TextSearch = "CLS02 Grade 11 History", Status = (int)ClassStatus.Active };
                context.Classes.AddRange(class1, class2);
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

                var service = new ClassService(classRepo, courseRepo, teacherRepo, studentRepo, tsRepo, schRepo, userManager, roleManager, context, mockOpt.Object, Mock.Of<INotificationService>());

                var searchDto = new ClassSearchDto
                {
                    Keyword = "Math",
                    PageIndex = 1,
                    PageSize = 10
                };

                var response = await service.GetAllAsync(searchDto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data.Should().NotBeNull();
                response.Data!.Items.Should().HaveCount(1);
                response.Data.Items.First().Code.Should().Be("CLS01");
            }
        }

        [Fact]
        public async Task Normal_GetByIdAsync_ShouldReturnClassDetail()
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

                var service = new ClassService(classRepo, courseRepo, teacherRepo, studentRepo, tsRepo, schRepo, userManager, roleManager, context, mockOpt.Object, Mock.Of<INotificationService>());

                var response = await service.GetByIdAsync(classId);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data.Should().NotBeNull();
                response.Data!.Id.Should().Be(classId);
            }
        }

        [Fact]
        public async Task Normal_CreateAsync_ShouldCreateClassAndSchedules()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            var mockOpt = GetMockOptimizationService();

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var course = new Course { Code = "KH00001", Name = "Math Course", Status = 1 };
                var teacher = new Teacher { Code = "GV00001", Name = "Mr. Test", Email = "mrtest@test.com", Status = 1 };
                var student = new Student { Code = "HS001", Name = "Student One", Email = "stud1@test.com", Status = 1 };
                context.Courses.Add(course);
                context.Teachers.Add(teacher);
                context.Students.Add(student);
                await context.SaveChangesAsync();
            }

            var saveDto = new ClassSaveDto
            {
                Code = "CLS_TEST",
                Name = "Test Class",
                StartDate = DateTime.UtcNow.Date,
                ExpectedLessons = 2,
                CourseId = 1,
                TeacherId = 1,
                Students = new List<StudentEnrollDto> { new StudentEnrollDto { StudentId = 1 } },
                WeeklySchedules = new List<WeeklyScheduleDto>
                {
                    new WeeklyScheduleDto
                    {
                        DayOfWeek = (int)DateTime.UtcNow.DayOfWeek,
                        StartTime = "08:00",
                        EndTime = "10:00",
                        RoomId = 1
                    }
                }
            };

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

                var service = new ClassService(classRepo, courseRepo, teacherRepo, studentRepo, tsRepo, schRepo, userManager, roleManager, context, mockOpt.Object, Mock.Of<INotificationService>());

                var response = await service.CreateAsync(saveDto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data.Should().NotBeNull();
                response.Data!.Code.Should().Be("CLS_TEST");
                
                var classInDb = await context.Classes
                    .Include(c => c.ClassSchedules)
                    .Include(c => c.StudentClasses)
                    .FirstOrDefaultAsync(c => c.Code == "CLS_TEST");
                classInDb.Should().NotBeNull();
                classInDb!.StudentClasses.Should().HaveCount(1);
                classInDb.ClassSchedules.Should().HaveCount(2);
            }
        }

        [Fact]
        public async Task Normal_EditAsync_ShouldUpdateClassAndSchedules()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            var mockOpt = GetMockOptimizationService();
            int classId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var course = new Course { Code = "KH00001", Name = "Math Course", Status = 1 };
                var teacher = new Teacher { Code = "GV00001", Name = "Mr. Test", Email = "mrtest@test.com", Status = 1 };
                var student = new Student { Code = "HS001", Name = "Student One", Email = "stud1@test.com", Status = 1 };
                context.Courses.Add(course);
                context.Teachers.Add(teacher);
                context.Students.Add(student);

                var cls = new Class
                {
                    Code = "CLS01",
                    Name = "Old Class Name",
                    StartDate = DateTime.UtcNow.Date,
                    ExpectedLessons = 2,
                    CourseId = course.Id,
                    TeacherId = teacher.Id
                };
                context.Classes.Add(cls);
                await context.SaveChangesAsync();
                classId = cls.Id;
            }

            var saveDto = new ClassSaveDto
            {
                Id = classId,
                Code = "CLS01",
                Name = "Updated Class Name",
                StartDate = DateTime.UtcNow.Date,
                ExpectedLessons = 3,
                CourseId = 1,
                TeacherId = 1,
                Students = new List<StudentEnrollDto> { new StudentEnrollDto { StudentId = 1 } },
                WeeklySchedules = new List<WeeklyScheduleDto>
                {
                    new WeeklyScheduleDto
                    {
                        DayOfWeek = (int)DateTime.UtcNow.DayOfWeek,
                        StartTime = "08:00",
                        EndTime = "10:00",
                        RoomId = 1
                    }
                }
            };

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

                var service = new ClassService(classRepo, courseRepo, teacherRepo, studentRepo, tsRepo, schRepo, userManager, roleManager, context, mockOpt.Object, Mock.Of<INotificationService>());

                var response = await service.EditAsync(saveDto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data!.Name.Should().Be("Updated Class Name");

                var updated = await context.Classes
                    .Include(c => c.ClassSchedules)
                    .FirstOrDefaultAsync(c => c.Id == classId);
                updated!.ClassSchedules.Should().HaveCount(3);
            }
        }

        [Fact]
        public async Task Normal_DeleteAsync_ShouldDeleteClassAndResetRegistrations()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            var mockOpt = GetMockOptimizationService();
            int classId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var course = new Course { Code = "CRS01", Name = "Math", Status = 1 };
                var teacher = new Teacher { Code = "TCH01", Name = "Teacher", Email = "t@test.com", Status = 1 };
                var semester = new Semester { Code = "SEM01", Name = "Semester", StartDate = DateTime.UtcNow.Date, EndDate = DateTime.UtcNow.Date.AddMonths(3) };
                context.Courses.Add(course);
                context.Teachers.Add(teacher);
                context.Semesters.Add(semester);
                await context.SaveChangesAsync();

                var cls = new Class
                {
                    Code = "CLS01",
                    Name = "Class",
                    StartDate = semester.StartDate,
                    EndDate = semester.EndDate,
                    CourseId = course.Id,
                    TeacherId = teacher.Id,
                    SemesterId = semester.Id
                };
                context.Classes.Add(cls);
                await context.SaveChangesAsync();
                classId = cls.Id;

                var student = new Student { Code = "ST001", Name = "Student", Email = "s@test.com", Status = 1 };
                context.Students.Add(student);
                await context.SaveChangesAsync();

                context.StudentClasses.Add(new StudentClass { ClassId = classId, StudentId = student.Id, Status = (int)StudentClassStatus.Enrolled });
                context.StudentRegistrations.Add(new StudentRegistration
                {
                    SemesterId = semester.Id,
                    CourseId = course.Id,
                    StudentId = student.Id,
                    Status = (int)StudentRegistrationStatus.Scheduled,
                    PreferredSlotsJson = "[]"
                });
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

                var service = new ClassService(classRepo, courseRepo, teacherRepo, studentRepo, tsRepo, schRepo, userManager, roleManager, context, mockOpt.Object, Mock.Of<INotificationService>());

                var response = await service.DeleteAsync(classId);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();

                var deletedClass = await context.Classes.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == classId);
                deletedClass!.IsDeleted.Should().BeTrue();

                var registration = await context.StudentRegistrations.FirstOrDefaultAsync();
                registration!.Status.Should().Be((int)StudentRegistrationStatus.Pending);
            }
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

                var service = new ClassService(classRepo, courseRepo, teacherRepo, studentRepo, tsRepo, schRepo, userManager, roleManager, context, mockOpt.Object, Mock.Of<INotificationService>());

                var response = await service.DeactiveAsync(classId);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();

                var deactivated = await context.Classes.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == classId);
                deactivated!.IsDeleted.Should().BeTrue();
            }
        }

        #endregion

        #region Boundary Test Cases (Kiểm thử giá trị biên)

        [Fact]
        public async Task Boundary_CreateAsync_WithCodeExactly50Chars_ShouldSucceed()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            var mockOpt = GetMockOptimizationService();

            var longCode = new string('A', 50);

            var saveDto = new ClassSaveDto
            {
                Code = longCode,
                Name = "Valid Name",
                StartDate = DateTime.UtcNow.Date,
                ExpectedLessons = 1,
                WeeklySchedules = new List<WeeklyScheduleDto>
                {
                    new WeeklyScheduleDto
                    {
                        DayOfWeek = (int)DateTime.UtcNow.DayOfWeek,
                        StartTime = "08:00",
                        EndTime = "10:00",
                        RoomId = 1
                    }
                }
            };

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

                var service = new ClassService(classRepo, courseRepo, teacherRepo, studentRepo, tsRepo, schRepo, userManager, roleManager, context, mockOpt.Object, Mock.Of<INotificationService>());

                var response = await service.CreateAsync(saveDto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data!.Code.Length.Should().Be(50);
            }
        }

        [Fact]
        public async Task Boundary_CreateAsync_WithNameExactly200Chars_ShouldSucceed()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            var mockOpt = GetMockOptimizationService();

            var longName = new string('B', 200);

            var saveDto = new ClassSaveDto
            {
                Code = "VAL01",
                Name = longName,
                StartDate = DateTime.UtcNow.Date,
                ExpectedLessons = 1,
                WeeklySchedules = new List<WeeklyScheduleDto>
                {
                    new WeeklyScheduleDto
                    {
                        DayOfWeek = (int)DateTime.UtcNow.DayOfWeek,
                        StartTime = "08:00",
                        EndTime = "10:00",
                        RoomId = 1
                    }
                }
            };

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

                var service = new ClassService(classRepo, courseRepo, teacherRepo, studentRepo, tsRepo, schRepo, userManager, roleManager, context, mockOpt.Object, Mock.Of<INotificationService>());

                var response = await service.CreateAsync(saveDto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data!.Name.Length.Should().Be(200);
            }
        }

        #endregion

        #region Abnormal Test Cases (Kiểm thử giá trị bất thường)

        [Fact]
        public async Task Abnormal_CreateAsync_DuplicateCode_ShouldReturnFail()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            var mockOpt = GetMockOptimizationService();

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var cls = new Class { Code = "DUP01", Name = "First Class", Status = (int)ClassStatus.Active };
                context.Classes.Add(cls);
                await context.SaveChangesAsync();
            }

            var saveDto = new ClassSaveDto
            {
                Code = "DUP01", // Duplicate Code
                Name = "Second Class",
                StartDate = DateTime.UtcNow.Date,
                ExpectedLessons = 1
            };

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

                var service = new ClassService(classRepo, courseRepo, teacherRepo, studentRepo, tsRepo, schRepo, userManager, roleManager, context, mockOpt.Object, Mock.Of<INotificationService>());

                var response = await service.CreateAsync(saveDto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.Message.Should().Be("ERR_CODE_DUPLICATE");
            }
        }

        [Fact]
        public async Task Abnormal_CreateAsync_TeacherNotFound_ShouldReturnFail()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            var mockOpt = GetMockOptimizationService();

            var saveDto = new ClassSaveDto
            {
                Code = "CLS01",
                Name = "Class Name",
                StartDate = DateTime.UtcNow.Date,
                ExpectedLessons = 1,
                TeacherId = 9999 // Non-existent Teacher Id
            };

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

                var service = new ClassService(classRepo, courseRepo, teacherRepo, studentRepo, tsRepo, schRepo, userManager, roleManager, context, mockOpt.Object, Mock.Of<INotificationService>());

                var response = await service.CreateAsync(saveDto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.Message.Should().Be("ERR_TEACHER_NOT_FOUND");
            }
        }

        [Fact]
        public async Task Abnormal_DeleteAsync_NotFound_ShouldReturnNotFound()
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

                var service = new ClassService(classRepo, courseRepo, teacherRepo, studentRepo, tsRepo, schRepo, userManager, roleManager, context, mockOpt.Object, Mock.Of<INotificationService>());

                var response = await service.DeleteAsync(9999);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.Message.Should().Be("ERR_CLASS_NOT_FOUND");
            }
        }

        [Fact]
        public async Task Abnormal_IntentionalFailure_ShouldFail()
        {
            // Arrange
            // This test case is written to fail intentionally to verify that the unit test runner
            // captures failing assertions correctly, as requested by the user.
            var value = true;

            // Assert
            value.Should().BeFalse("This is an intentional failure to prove that the test suite is capable of identifying failures.");
        }

        #endregion
    }
}
