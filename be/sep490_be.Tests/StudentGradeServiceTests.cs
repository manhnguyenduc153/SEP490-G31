using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using sep490_be.DTO.StudentGrade;
using sep490_be.Enums;
using sep490_be.Models;
using sep490_be.Services.Implementations;
using ModelClass = sep490_be.Models.Class;

namespace sep490_be.Tests.Services
{
    public class StudentGradeServiceTests
    {
        private static DbContextOptions<ApplicationDbContext> CreateNewContextOptions()
        {
            return new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }

        private static Mock<IHttpContextAccessor> GetMockHttpContextAccessor()
        {
            return new Mock<IHttpContextAccessor>();
        }

        private static async Task<(int courseId, int classId, int studentClassId)> SeedClassAsync(ApplicationDbContext context)
        {
            var course = new Course { Code = "C001", Name = "IELTS Foundation", Status = (int)GeneralStatus.Active };
            var student = new Student { Code = "ST001", Name = "Student One", Email = "student@test.com", Status = (int)StudentStatus.Active };
            var classEntity = new ModelClass { Code = "CL001", Name = "Class One", Course = course, Status = (int)ClassStatus.Active };
            var studentClass = new StudentClass { Student = student, Class = classEntity, Status = (int)StudentClassStatus.Studying };

            context.Courses.Add(course);
            context.Students.Add(student);
            context.Classes.Add(classEntity);
            context.StudentClasses.Add(studentClass);
            await context.SaveChangesAsync();

            return (course.Id, classEntity.Id, studentClass.Id);
        }

        [Fact]
        public async Task GetSettingsAsync_WhenClassHasCourse_ShouldCreateDefaultComponents()
        {
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            using var context = new ApplicationDbContext(options, mockHttp.Object);
            var (_, classId, _) = await SeedClassAsync(context);
            var service = new StudentGradeService(context);

            var response = await service.GetSettingsAsync(classId);

            response.Success.Should().BeTrue();
            response.Data!.ClassId.Should().Be(classId);
            response.Data.Components.Select(x => x.Code).Should().Equal("attendance", "homework", "exam");
            response.Data.Components.Select(x => x.Weight).Should().Equal(30m, 30m, 40m);
        }

        [Fact]
        public async Task GetSettingsAsync_WhenClassDoesNotExist_ShouldReturnNotFound()
        {
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            using var context = new ApplicationDbContext(options, mockHttp.Object);
            var service = new StudentGradeService(context);

            var response = await service.GetSettingsAsync(9999);

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
            response.Message.Should().Be("ERR_CLASS_NOT_FOUND");
        }

        [Fact]
        public async Task GetCourseComponentsAsync_WhenCourseExistsWithoutComponents_ShouldCreateDefaults()
        {
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            using var context = new ApplicationDbContext(options, mockHttp.Object);
            var course = new Course { Code = "C001", Name = "Course One", Status = (int)GeneralStatus.Active };
            context.Courses.Add(course);
            await context.SaveChangesAsync();

            var service = new StudentGradeService(context);
            var response = await service.GetCourseComponentsAsync(course.Id);

            response.Success.Should().BeTrue();
            response.Data.Should().HaveCount(3);
            response.Data!.Select(x => x.Code).Should().Equal("attendance", "homework", "exam");
        }

        [Fact]
        public async Task SaveCourseComponentsAsync_WithValidComponents_ShouldCreateAndOrderComponents()
        {
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            using var context = new ApplicationDbContext(options, mockHttp.Object);
            var course = new Course { Code = "C001", Name = "Course One", Status = (int)GeneralStatus.Active };
            context.Courses.Add(course);
            await context.SaveChangesAsync();

            var service = new StudentGradeService(context);
            var response = await service.SaveCourseComponentsAsync(course.Id, new ClassGradeComponentsSaveDto
            {
                Components = new List<GradeComponentSaveDto>
                {
                    new() { Code = "quiz", Name = "Quiz", Weight = 20, SortOrder = 2 },
                    new() { Code = "final", Name = "Final Exam", Weight = 80, SortOrder = 1, IsSystem = true }
                }
            });

            response.Success.Should().BeTrue();
            response.Data!.Select(x => x.Code).Should().Equal("final", "quiz");
            response.Data!.Sum(x => x.Weight).Should().Be(100);
        }

        [Fact]
        public async Task SaveCourseComponentsAsync_WithEmptyList_ShouldReturnBadRequest()
        {
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            using var context = new ApplicationDbContext(options, mockHttp.Object);
            var course = new Course { Code = "C001", Name = "Course One", Status = (int)GeneralStatus.Active };
            context.Courses.Add(course);
            await context.SaveChangesAsync();

            var service = new StudentGradeService(context);
            var response = await service.SaveCourseComponentsAsync(course.Id, new ClassGradeComponentsSaveDto());

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
            response.Message.Should().Be("ERR_GRADE_COMPONENT_EMPTY");
        }

        [Fact]
        public async Task SaveOverridesAsync_ShouldClampScoresAndIgnoreInvalidTargets()
        {
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            using var context = new ApplicationDbContext(options, mockHttp.Object);
            var (_, classId, studentClassId) = await SeedClassAsync(context);
            var service = new StudentGradeService(context);
            var settings = await service.GetSettingsAsync(classId);
            var attendanceId = settings.Data!.Components.First(x => x.Code == "attendance").Id;
            var examId = settings.Data.Components.First(x => x.Code == "exam").Id;

            var response = await service.SaveOverridesAsync(classId, new StudentGradeOverridesSaveDto
            {
                Overrides = new List<StudentGradeOverrideSaveDto>
                {
                    new() { StudentClassId = studentClassId, GradeComponentId = attendanceId, Score = 12 },
                    new() { StudentClassId = studentClassId, GradeComponentId = examId, Score = -2 },
                    new() { StudentClassId = 9999, GradeComponentId = attendanceId, Score = 8 }
                }
            });

            response.Success.Should().BeTrue();
            response.Data.Should().HaveCount(2);
            response.Data!.First(x => x.GradeComponentId == attendanceId).Score.Should().Be(10);
            response.Data.First(x => x.GradeComponentId == examId).Score.Should().Be(0);
        }

        [Fact]
        public async Task SaveOverridesAsync_WithNullScore_ShouldRemoveExistingOverride()
        {
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            using var context = new ApplicationDbContext(options, mockHttp.Object);
            var (_, classId, studentClassId) = await SeedClassAsync(context);
            var service = new StudentGradeService(context);
            var settings = await service.GetSettingsAsync(classId);
            var attendanceId = settings.Data!.Components.First(x => x.Code == "attendance").Id;

            await service.SaveOverridesAsync(classId, new StudentGradeOverridesSaveDto
            {
                Overrides = new List<StudentGradeOverrideSaveDto>
                {
                    new() { StudentClassId = studentClassId, GradeComponentId = attendanceId, Score = 8 }
                }
            });

            var response = await service.SaveOverridesAsync(classId, new StudentGradeOverridesSaveDto
            {
                Overrides = new List<StudentGradeOverrideSaveDto>
                {
                    new() { StudentClassId = studentClassId, GradeComponentId = attendanceId, Score = null }
                }
            });

            response.Success.Should().BeTrue();
            response.Data.Should().BeEmpty();
            var visibleOverrideCount = await context.StudentGradeOverrides.CountAsync();
            var totalOverrideCount = await context.StudentGradeOverrides.IgnoreQueryFilters().CountAsync();
            visibleOverrideCount.Should().Be(0);
            totalOverrideCount.Should().Be(1);
        }
    }
}
