using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using sep490_be.DTO.StudentGrade;
using sep490_be.Enums;
using sep490_be.Models;
using sep490_be.Repositories.Common;
using sep490_be.Repositories.Implementations;
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

        private static StudentGradeService CreateService(ApplicationDbContext context)
        {
            var store = new Mock<IUserStore<IdentityUser>>();
            var userManager = new Mock<UserManager<IdentityUser>>(
                store.Object, null!, null!, Array.Empty<IUserValidator<IdentityUser>>(),
                Array.Empty<IPasswordValidator<IdentityUser>>(), null!, null!, null!, null!);
            return new StudentGradeService(
                new StudentGradeRepository(context, new UnitOfWork<ApplicationDbContext>(context)),
                userManager.Object);
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

        #region Normal Test Cases (Kiểm thử giá trị thông thường)

        [Fact]
        public async Task GetSettingsAsync_WhenClassHasCourse_ShouldCreateDefaultComponents()
        {
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            using var context = new ApplicationDbContext(options, mockHttp.Object);
            var (_, classId, _) = await SeedClassAsync(context);
            var service = CreateService(context);

            var response = await service.GetSettingsAsync(classId);

            response.Success.Should().BeTrue();
            response.Data!.ClassId.Should().Be(classId);
            response.Data.Components.Select(x => x.Code).Should().Equal(
                "listening", "reading", "writing", "speaking", "homework");
            response.Data.Components.Select(x => x.Weight).Should().Equal(
                17.5m, 17.5m, 17.5m, 17.5m, 30m);
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

            var service = CreateService(context);
            var response = await service.GetCourseComponentsAsync(course.Id);

            response.Success.Should().BeTrue();
            response.Data.Should().HaveCount(5);
            response.Data!.Select(x => x.Code).Should().Equal(
                "listening", "reading", "writing", "speaking", "homework");
        }

        [Fact]
        public async Task GetCourseComponentsAsync_WithLegacyExamComponent_ShouldReplaceItWithFourSkills()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var course = new Course { Code = "C-LEGACY", Name = "Legacy Course", Status = (int)GeneralStatus.Active };
            context.Courses.Add(course);
            await context.SaveChangesAsync();
            context.GradeComponents.AddRange(
                new GradeComponent { CourseId = course.Id, Code = "attendance", Name = "Attendance", Weight = 30, SortOrder = 1, IsSystem = true },
                new GradeComponent { CourseId = course.Id, Code = "homework", Name = "Homework", Weight = 30, SortOrder = 2, IsSystem = true },
                new GradeComponent { CourseId = course.Id, Code = "exam", Name = "Exam", Weight = 40, SortOrder = 3, IsSystem = true });
            await context.SaveChangesAsync();

            var response = await CreateService(context).GetCourseComponentsAsync(course.Id);

            response.Success.Should().BeTrue();
            response.Data!.Select(x => x.Code).Should().Equal(
                "listening", "reading", "writing", "speaking", "homework");
            response.Data!.Sum(x => x.Weight).Should().Be(100);
            response.Data!.Where(x => x.Code != "homework").Select(x => x.Weight).Should().AllBeEquivalentTo(17.5m);
            (await context.GradeComponents.IgnoreQueryFilters()
                .SingleAsync(x => x.CourseId == course.Id && x.Code == "exam"))
                .IsDeleted.Should().BeTrue();
            (await context.GradeComponents.IgnoreQueryFilters()
                .AnyAsync(x => x.CourseId == course.Id && x.Code == "attendance"))
                .Should().BeFalse();
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

            var service = CreateService(context);
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
        public async Task GetMyGradesAsync_ShouldCalculateRawScoresAndWeightedAverage()
        {
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            using var context = new ApplicationDbContext(options, mockHttp.Object);
            var (_, classId, studentClassId) = await SeedClassAsync(context);
            var studentId = (await context.StudentClasses.FindAsync(studentClassId))!.StudentId;
            var service = CreateService(context);
            var settings = await service.GetSettingsAsync(classId);

            var teacher = new Teacher { Code = "TC001", Name = "Teacher One", Email = "teacher@test.com" };
            context.Teachers.Add(teacher);
            await context.SaveChangesAsync();
            var homework1 = new Homework { ClassId = classId, TeacherId = teacher.Id, Title = "HW1", TotalScore = 10, Status = 1 };
            var homework2 = new Homework { ClassId = classId, TeacherId = teacher.Id, Title = "HW2", TotalScore = 20, Status = 1 };
            context.Homeworks.AddRange(homework1, homework2);

            await context.SaveChangesAsync();
            context.HomeworkSubmissions.AddRange(
                new HomeworkSubmission { HomeworkId = homework1.Id, StudentId = studentId, SubmitTime = DateTime.UtcNow, Score = 8, Status = 2 },
                new HomeworkSubmission { HomeworkId = homework2.Id, StudentId = studentId, SubmitTime = DateTime.UtcNow, Score = 10, Status = 2 });
            await context.SaveChangesAsync();

            var response = await service.GetMyGradesAsync(new[] { "student" });

            response.Success.Should().BeTrue();
            response.Data.Should().ContainSingle();
            var grade = response.Data!.Single();
            grade.Components.Single(x => x.ComponentCode == "homework").RawScore.Should().Be(6.5m);
            grade.Components.Where(x => new[] { "listening", "reading", "writing", "speaking" }.Contains(x.ComponentCode))
                .Should().OnlyContain(x => x.RawScore == 0);
            grade.AverageScore.Should().Be(2m);
            settings.Data!.Components.Should().HaveCount(5);
        }
        [Fact]
        public async Task GetMyGradesAsync_WhenOverrideExists_ShouldUseOverrideButKeepRawScore()
        {
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            using var context = new ApplicationDbContext(options, mockHttp.Object);
            var (_, classId, studentClassId) = await SeedClassAsync(context);
            var service = CreateService(context);
            var settings = await service.GetSettingsAsync(classId);
            var speaking = settings.Data!.Components.Single(x => x.Code == "speaking");
            await service.SaveOverridesAsync(classId, new StudentGradeOverridesSaveDto
            {
                Overrides = new List<StudentGradeOverrideSaveDto>
                {
                    new() { StudentClassId = studentClassId, GradeComponentId = speaking.Id, Score = 9 }
                }
            });

            var response = await service.GetMyGradesAsync(new[] { "ST001" });

            var component = response.Data!.Single().Components.Single(x => x.ComponentCode == "speaking");
            component.RawScore.Should().Be(0);
            component.Score.Should().Be(9);
            component.IsOverride.Should().BeTrue();
            response.Data!.Single().AverageScore.Should().Be(1.6m);
        }

        #endregion

        #region Boundary Test Cases (Kiểm thử giá trị biên)

        [Fact]
        public async Task SaveOverridesAsync_WithOutOfRangeScores_ShouldReturnBadRequest()
        {
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            using var context = new ApplicationDbContext(options, mockHttp.Object);
            var (_, classId, studentClassId) = await SeedClassAsync(context);
            var service = CreateService(context);
            var settings = await service.GetSettingsAsync(classId);
            var speakingId = settings.Data!.Components.First(x => x.Code == "speaking").Id;
            var listeningId = settings.Data.Components.First(x => x.Code == "listening").Id;

            var response = await service.SaveOverridesAsync(classId, new StudentGradeOverridesSaveDto
            {
                Overrides = new List<StudentGradeOverrideSaveDto>
                {
                    new() { StudentClassId = studentClassId, GradeComponentId = speakingId, Score = 12 },
                    new() { StudentClassId = studentClassId, GradeComponentId = listeningId, Score = -2 }
                }
            });

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
            response.Message.Should().Be("ERR_GRADE_SCORE_RANGE");
            (await context.StudentGradeOverrides.CountAsync()).Should().Be(0);
        }

        [Fact]
        public async Task SaveOverridesAsync_WithStudentOutsideClass_ShouldReturnNotFound()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var (_, classId, _) = await SeedClassAsync(context);
            var service = CreateService(context);
            var settings = await service.GetSettingsAsync(classId);
            var componentId = settings.Data!.Components.First().Id;

            var response = await service.SaveOverridesAsync(classId, new StudentGradeOverridesSaveDto
            {
                Overrides = new List<StudentGradeOverrideSaveDto>
                {
                    new() { StudentClassId = 9999, GradeComponentId = componentId, Score = 8 }
                }
            });

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
            response.Message.Should().Be("ERR_STUDENT_CLASS_NOT_FOUND");
        }
        [Fact]
        public async Task SaveOverridesAsync_WithNullScore_ShouldRemoveExistingOverride()
        {
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            using var context = new ApplicationDbContext(options, mockHttp.Object);
            var (_, classId, studentClassId) = await SeedClassAsync(context);
            var service = CreateService(context);
            var settings = await service.GetSettingsAsync(classId);
            var speakingId = settings.Data!.Components.First(x => x.Code == "speaking").Id;

            await service.SaveOverridesAsync(classId, new StudentGradeOverridesSaveDto
            {
                Overrides = new List<StudentGradeOverrideSaveDto>
                {
                    new() { StudentClassId = studentClassId, GradeComponentId = speakingId, Score = 8 }
                }
            });

            var response = await service.SaveOverridesAsync(classId, new StudentGradeOverridesSaveDto
            {
                Overrides = new List<StudentGradeOverrideSaveDto>
                {
                    new() { StudentClassId = studentClassId, GradeComponentId = speakingId, Score = null }
                }
            });

            response.Success.Should().BeTrue();
            response.Data.Should().BeEmpty();
            var visibleOverrideCount = await context.StudentGradeOverrides.CountAsync();
            var totalOverrideCount = await context.StudentGradeOverrides.IgnoreQueryFilters().CountAsync();
            visibleOverrideCount.Should().Be(0);
            totalOverrideCount.Should().Be(1);
        }

        #endregion

        #region Abnormal Test Cases (Kiểm thử giá trị bất thường / Lỗi)

        [Fact]
        public async Task GetSettingsAsync_WhenClassDoesNotExist_ShouldReturnNotFound()
        {
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            using var context = new ApplicationDbContext(options, mockHttp.Object);
            var service = CreateService(context);

            var response = await service.GetSettingsAsync(9999);

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
            response.Message.Should().Be("ERR_CLASS_NOT_FOUND");
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

            var service = CreateService(context);
            var response = await service.SaveCourseComponentsAsync(course.Id, new ClassGradeComponentsSaveDto());

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
            response.Message.Should().Be("ERR_GRADE_COMPONENT_EMPTY");
        }
        [Fact]
        public async Task GetMyGradesAsync_WithBlankIdentifiers_ShouldReturnStudentNotFound()
        {
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            using var context = new ApplicationDbContext(options, mockHttp.Object);

            var response = await CreateService(context).GetMyGradesAsync(new[] { "", "  " });

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
            response.Message.Should().Be("ERR_STUDENT_NOT_FOUND");
        }
        [Fact]
        public async Task SaveCourseComponentsAsync_WithBlankName_ShouldReturnBadRequest()
        {
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            using var context = new ApplicationDbContext(options, mockHttp.Object);
            var course = new Course { Code = "C001", Name = "Course One", Status = (int)GeneralStatus.Active };
            context.Courses.Add(course);
            await context.SaveChangesAsync();

            var response = await CreateService(context).SaveCourseComponentsAsync(course.Id, new ClassGradeComponentsSaveDto
            {
                Components = new List<GradeComponentSaveDto> { new() { Code = "quiz", Name = "  ", Weight = 10 } }
            });

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
            response.Message.Should().Be("ERR_GRADE_COMPONENT_NAME_EMPTY");
        }

        [Fact]
        public async Task GetSettingsAsync_WhenClassHasNoCourse_ShouldReturnBadRequest()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var classEntity = new ModelClass { Code = "NO_COURSE", Name = "No course", Status = (int)ClassStatus.Active };
            context.Classes.Add(classEntity);
            await context.SaveChangesAsync();

            var response = await CreateService(context).GetSettingsAsync(classEntity.Id);

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
            response.Message.Should().Be("ERR_CLASS_COURSE_NOT_FOUND");
        }

        [Fact]
        public async Task GetCourseComponentsAsync_WhenCourseDoesNotExist_ShouldReturnNotFound()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);

            var response = await CreateService(context).GetCourseComponentsAsync(9999);

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
            response.Message.Should().Be("ERR_COURSE_NOT_FOUND");
        }

        [Fact]
        public async Task SaveCourseComponentsAsync_WhenCourseDoesNotExist_ShouldReturnNotFound()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);

            var response = await CreateService(context).SaveCourseComponentsAsync(9999, new ClassGradeComponentsSaveDto
            {
                Components = new List<GradeComponentSaveDto> { new() { Name = "Quiz", Weight = 10 } }
            });

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
            response.Message.Should().Be("ERR_COURSE_NOT_FOUND");
        }

        [Fact]
        public async Task SaveCourseComponentsAsync_WithNegativeWeight_ShouldReturnBadRequest()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var course = new Course { Code = "C_NEG", Name = "Negative", Status = (int)GeneralStatus.Active };
            context.Courses.Add(course);
            await context.SaveChangesAsync();

            var response = await CreateService(context).SaveCourseComponentsAsync(course.Id, new ClassGradeComponentsSaveDto
            {
                Components = new List<GradeComponentSaveDto> { new() { Name = "  Quiz  ", Code = " ", Weight = -5, SortOrder = 0 } }
            });

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
            response.Message.Should().Be("ERR_GRADE_COMPONENT_WEIGHT_RANGE");
        }

        [Fact]
        public async Task SaveCourseComponentsAsync_WhenTotalWeightIsNot100_ShouldReturnBadRequestAndNotUpdate()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var course = new Course { Code = "C_WEIGHT", Name = "Weight Validation", Status = (int)GeneralStatus.Active };
            context.Courses.Add(course);
            await context.SaveChangesAsync();

            var existing = new List<GradeComponent>
            {
                new() { CourseId = course.Id, Code = "MID", Name = "Midterm", Weight = 40, SortOrder = 1 },
                new() { CourseId = course.Id, Code = "FIN", Name = "Final", Weight = 60, SortOrder = 2 }
            };
            context.GradeComponents.AddRange(existing);
            await context.SaveChangesAsync();

            var response = await CreateService(context).SaveCourseComponentsAsync(course.Id, new ClassGradeComponentsSaveDto
            {
                Components = new List<GradeComponentSaveDto>
                {
                    new() { Id = existing[0].Id, Code = "MID", Name = "Changed Midterm", Weight = 40, SortOrder = 2 },
                    new() { Id = existing[1].Id, Code = "FIN", Name = "Changed Final", Weight = 40, SortOrder = 1 }
                }
            });

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
            response.Message.Should().Be("ERR_TOTAL_WEIGHT_MUST_BE_100");

            var persisted = await context.GradeComponents
                .AsNoTracking()
                .Where(x => x.CourseId == course.Id)
                .OrderBy(x => x.SortOrder)
                .ToListAsync();
            persisted.Select(x => x.Name).Should().Equal("Midterm", "Final");
            persisted.Select(x => x.Weight).Should().Equal(40, 60);
            persisted.Select(x => x.SortOrder).Should().Equal(1, 2);
        }

        [Fact]
        public async Task SaveCourseComponentsAsync_WithDuplicateCodes_ShouldReturnBadRequestAndNotUpdate()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var course = new Course { Code = "C_DUP", Name = "Duplicate Code", Status = (int)GeneralStatus.Active };
            context.Courses.Add(course);
            await context.SaveChangesAsync();

            var existing = new List<GradeComponent>
            {
                new() { CourseId = course.Id, Code = "MID", Name = "Midterm", Weight = 40, SortOrder = 1 },
                new() { CourseId = course.Id, Code = "FIN", Name = "Final", Weight = 60, SortOrder = 2 }
            };
            context.GradeComponents.AddRange(existing);
            await context.SaveChangesAsync();

            var response = await CreateService(context).SaveCourseComponentsAsync(course.Id, new ClassGradeComponentsSaveDto
            {
                Components = new List<GradeComponentSaveDto>
                {
                    new() { Id = existing[0].Id, Code = "MID", Name = "Changed Midterm", Weight = 40, SortOrder = 2 },
                    new() { Id = existing[1].Id, Code = "mid", Name = "Changed Final", Weight = 60, SortOrder = 1 }
                }
            });

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
            response.Message.Should().Be("ERR_COMPONENT_CODE_DUPLICATE");

            var persisted = await context.GradeComponents
                .AsNoTracking()
                .Where(x => x.CourseId == course.Id)
                .OrderBy(x => x.SortOrder)
                .ToListAsync();
            persisted.Select(x => x.Code).Should().Equal("MID", "FIN");
            persisted.Select(x => x.Name).Should().Equal("Midterm", "Final");
            persisted.Select(x => x.Weight).Should().Equal(40, 60);
            persisted.Select(x => x.SortOrder).Should().Equal(1, 2);
        }

        [Fact]
        public async Task SaveCourseComponentsAsync_ShouldKeepOmittedSystemComponents()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var course = new Course { Code = "C_UPD", Name = "Update", Status = (int)GeneralStatus.Active };
            context.Courses.Add(course);
            await context.SaveChangesAsync();
            var service = CreateService(context);
            var defaults = (await service.GetCourseComponentsAsync(course.Id)).Data!;
            var kept = defaults.First();

            var response = await service.SaveCourseComponentsAsync(course.Id, new ClassGradeComponentsSaveDto
            {
                Components = new List<GradeComponentSaveDto>
                {
                    new() { Id = kept.Id, Code = kept.Code, Name = "Updated listening", Weight = kept.Weight, SortOrder = 1, IsSystem = true }
                }
            });

            response.Success.Should().BeTrue();
            response.Data.Should().HaveCount(5);
            response.Data!.First(x => x.Id == kept.Id).Name.Should().Be("Updated listening");
            (await context.GradeComponents.CountAsync(x => x.CourseId == course.Id)).Should().Be(5);
        }

        [Fact]
        public async Task SaveOverridesAsync_WhenClassDoesNotExist_ShouldReturnNotFound()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);

            var response = await CreateService(context).SaveOverridesAsync(9999, new StudentGradeOverridesSaveDto());

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
            response.Message.Should().Be("ERR_CLASS_NOT_FOUND");
        }

        [Fact]
        public async Task SaveOverridesAsync_WithEmptyOverrides_ShouldKeepExistingOverrides()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var (_, classId, studentClassId) = await SeedClassAsync(context);
            var service = CreateService(context);
            var componentId = (await service.GetSettingsAsync(classId)).Data!.Components.First().Id;
            await service.SaveOverridesAsync(classId, new StudentGradeOverridesSaveDto
            {
                Overrides = new List<StudentGradeOverrideSaveDto> { new() { StudentClassId = studentClassId, GradeComponentId = componentId, Score = 7 } }
            });

            var response = await service.SaveOverridesAsync(classId, new StudentGradeOverridesSaveDto());

            response.Success.Should().BeTrue();
            response.Data.Should().ContainSingle(x => x.Score == 7);
        }

        [Fact]
        public async Task SaveCourseComponentsAsync_WithDuplicateIds_ShouldReturnBadRequestAndNotUpdate()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var (courseId, _, _) = await SeedClassAsync(context);
            var service = CreateService(context);
            var components = (await service.GetCourseComponentsAsync(courseId)).Data!;
            var first = components[0];

            var response = await service.SaveCourseComponentsAsync(courseId, new ClassGradeComponentsSaveDto
            {
                Components = new List<GradeComponentSaveDto>
                {
                    new() { Id = first.Id, Code = first.Code, Name = "First update", Weight = 50, SortOrder = 1 },
                    new() { Id = first.Id, Code = first.Code, Name = "Duplicate update", Weight = 50, SortOrder = 2 }
                }
            });

            response.Success.Should().BeFalse();
            response.Message.Should().Be("ERR_GRADE_COMPONENT_DUPLICATE");
            (await context.GradeComponents.AsNoTracking().SingleAsync(x => x.Id == first.Id))
                .Name.Should().Be(first.Name);
        }

        [Fact]
        public async Task SaveCourseComponentsAsync_WithComponentIdFromOutsideCourse_ShouldReturnBadRequest()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var (courseId, _, _) = await SeedClassAsync(context);
            var otherCourse = new Course { Code = "C-OTHER", Name = "Other", Status = (int)GeneralStatus.Active };
            var foreignComponent = new GradeComponent
            {
                Course = otherCourse,
                Code = "FOREIGN",
                Name = "Foreign",
                Weight = 100,
                SortOrder = 1
            };
            context.Add(foreignComponent);
            await context.SaveChangesAsync();

            var response = await CreateService(context).SaveCourseComponentsAsync(courseId, new ClassGradeComponentsSaveDto
            {
                Components = new List<GradeComponentSaveDto>
                {
                    new() { Id = foreignComponent.Id, Code = "FOREIGN", Name = "Foreign", Weight = 100, SortOrder = 1 }
                }
            });

            response.Success.Should().BeFalse();
            response.Message.Should().Be("ERR_GRADE_COMPONENT_INVALID");
        }

        [Theory]
        [InlineData(-0.01)]
        [InlineData(100.01)]
        public async Task SaveCourseComponentsAsync_WithWeightOutsideRange_ShouldReturnBadRequest(double weight)
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var (courseId, _, _) = await SeedClassAsync(context);

            var response = await CreateService(context).SaveCourseComponentsAsync(courseId, new ClassGradeComponentsSaveDto
            {
                Components = new List<GradeComponentSaveDto>
                {
                    new() { Code = "RANGE", Name = "Range", Weight = (decimal)weight, SortOrder = 1 }
                }
            });

            response.Success.Should().BeFalse();
            response.Message.Should().Be("ERR_GRADE_COMPONENT_WEIGHT_RANGE");
        }

        [Fact]
        public async Task SaveCourseComponentsAsync_WithNameOver200Characters_ShouldReturnBadRequest()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var (courseId, _, _) = await SeedClassAsync(context);

            var response = await CreateService(context).SaveCourseComponentsAsync(courseId, new ClassGradeComponentsSaveDto
            {
                Components = new List<GradeComponentSaveDto>
                {
                    new() { Code = "LONG_NAME", Name = new string('N', 201), Weight = 100, SortOrder = 1 }
                }
            });

            response.Success.Should().BeFalse();
            response.Message.Should().Be("ERR_GRADE_COMPONENT_NAME_MAX_LENGTH");
        }

        [Fact]
        public async Task SaveCourseComponentsAsync_WithCodeOver100Characters_ShouldReturnBadRequest()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var (courseId, _, _) = await SeedClassAsync(context);

            var response = await CreateService(context).SaveCourseComponentsAsync(courseId, new ClassGradeComponentsSaveDto
            {
                Components = new List<GradeComponentSaveDto>
                {
                    new() { Code = new string('C', 101), Name = "Long code", Weight = 100, SortOrder = 1 }
                }
            });

            response.Success.Should().BeFalse();
            response.Message.Should().Be("ERR_GRADE_COMPONENT_CODE_MAX_LENGTH");
        }

        [Fact]
        public async Task SaveOverridesAsync_WhenClassHasNoCourse_ShouldReturnBadRequest()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var classEntity = new ModelClass { Code = "CL-NC", Name = "No Course", Status = (int)ClassStatus.Active };
            context.Classes.Add(classEntity);
            await context.SaveChangesAsync();

            var response = await CreateService(context).SaveOverridesAsync(
                classEntity.Id,
                new StudentGradeOverridesSaveDto());

            response.Success.Should().BeFalse();
            response.Message.Should().Be("ERR_CLASS_COURSE_NOT_FOUND");
        }

        [Fact]
        public async Task SaveOverridesAsync_WithNullDto_ShouldReturnBadRequest()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var (_, classId, _) = await SeedClassAsync(context);

            var response = await CreateService(context).SaveOverridesAsync(classId, null!);

            response.Success.Should().BeFalse();
            response.Message.Should().Be("ERR_GRADE_OVERRIDE_INVALID");
        }

        [Fact]
        public async Task SaveOverridesAsync_WithDuplicateStudentComponentPair_ShouldReturnBadRequest()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var (_, classId, studentClassId) = await SeedClassAsync(context);
            var service = CreateService(context);
            var componentId = (await service.GetSettingsAsync(classId)).Data!.Components.First().Id;

            var response = await service.SaveOverridesAsync(classId, new StudentGradeOverridesSaveDto
            {
                Overrides = new List<StudentGradeOverrideSaveDto>
                {
                    new() { StudentClassId = studentClassId, GradeComponentId = componentId, Score = 7 },
                    new() { StudentClassId = studentClassId, GradeComponentId = componentId, Score = 8 }
                }
            });

            response.Success.Should().BeFalse();
            response.Message.Should().Be("ERR_GRADE_OVERRIDE_DUPLICATE");
            context.StudentGradeOverrides.Should().BeEmpty();
        }

        [Fact]
        public async Task SaveOverridesAsync_WithComponentFromAnotherCourse_ShouldReturnBadRequest()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var (_, classId, studentClassId) = await SeedClassAsync(context);
            var otherCourse = new Course { Code = "C-OVR", Name = "Other Override Course", Status = 1 };
            var foreignComponent = new GradeComponent
            {
                Course = otherCourse,
                Code = "FOREIGN",
                Name = "Foreign",
                Weight = 100,
                SortOrder = 1
            };
            context.Add(foreignComponent);
            await context.SaveChangesAsync();

            var response = await CreateService(context).SaveOverridesAsync(classId, new StudentGradeOverridesSaveDto
            {
                Overrides = new List<StudentGradeOverrideSaveDto>
                {
                    new() { StudentClassId = studentClassId, GradeComponentId = foreignComponent.Id, Score = 8 }
                }
            });

            response.Success.Should().BeFalse();
            response.Message.Should().Be("ERR_GRADE_COMPONENT_INVALID");
            context.StudentGradeOverrides.Should().BeEmpty();
        }

        [Fact]
        public async Task SaveOverridesAsync_WithBoundaryScores_ShouldCreateBothOverrides()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var (_, classId, studentClassId) = await SeedClassAsync(context);
            var service = CreateService(context);
            var components = (await service.GetSettingsAsync(classId)).Data!.Components;

            var response = await service.SaveOverridesAsync(classId, new StudentGradeOverridesSaveDto
            {
                Overrides = new List<StudentGradeOverrideSaveDto>
                {
                    new() { StudentClassId = studentClassId, GradeComponentId = components[0].Id, Score = 0 },
                    new() { StudentClassId = studentClassId, GradeComponentId = components[1].Id, Score = 10 }
                }
            });

            response.Success.Should().BeTrue();
            response.Data!.Select(x => x.Score).Should().BeEquivalentTo(new decimal?[] { 0, 10 });
            context.StudentGradeOverrides.Should().HaveCount(2);
        }

        [Fact]
        public async Task SaveOverridesAsync_WhenUpdatingExisting_ShouldKeepIdentityAndChangeScore()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var (_, classId, studentClassId) = await SeedClassAsync(context);
            var service = CreateService(context);
            var componentId = (await service.GetSettingsAsync(classId)).Data!.Components.First().Id;
            var first = await service.SaveOverridesAsync(classId, new StudentGradeOverridesSaveDto
            {
                Overrides = new List<StudentGradeOverrideSaveDto>
                {
                    new() { StudentClassId = studentClassId, GradeComponentId = componentId, Score = 6 }
                }
            });
            var overrideId = first.Data!.Single().Id;

            var response = await service.SaveOverridesAsync(classId, new StudentGradeOverridesSaveDto
            {
                Overrides = new List<StudentGradeOverrideSaveDto>
                {
                    new() { StudentClassId = studentClassId, GradeComponentId = componentId, Score = 9 }
                }
            });

            response.Success.Should().BeTrue();
            response.Data!.Single().Id.Should().Be(overrideId);
            response.Data!.Single().Score.Should().Be(9);
            context.StudentGradeOverrides.Should().ContainSingle();
        }

        [Fact]
        public async Task SaveOverridesAsync_WhenReAddingDeletedOverride_ShouldRestoreSameRecord()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var (_, classId, studentClassId) = await SeedClassAsync(context);
            var service = CreateService(context);
            var componentId = (await service.GetSettingsAsync(classId)).Data!.Components.First().Id;
            var created = await service.SaveOverridesAsync(classId, new StudentGradeOverridesSaveDto
            {
                Overrides = new List<StudentGradeOverrideSaveDto>
                {
                    new() { StudentClassId = studentClassId, GradeComponentId = componentId, Score = 5 }
                }
            });
            var originalId = created.Data!.Single().Id;
            await service.SaveOverridesAsync(classId, new StudentGradeOverridesSaveDto
            {
                Overrides = new List<StudentGradeOverrideSaveDto>
                {
                    new() { StudentClassId = studentClassId, GradeComponentId = componentId, Score = null }
                }
            });

            var response = await service.SaveOverridesAsync(classId, new StudentGradeOverridesSaveDto
            {
                Overrides = new List<StudentGradeOverrideSaveDto>
                {
                    new() { StudentClassId = studentClassId, GradeComponentId = componentId, Score = 8 }
                }
            });

            response.Success.Should().BeTrue();
            response.Data!.Single().Id.Should().Be(originalId);
            response.Data!.Single().Score.Should().Be(8);
            var restored = await context.StudentGradeOverrides.IgnoreQueryFilters().SingleAsync();
            restored.IsDeleted.Should().BeFalse();
        }

        [Fact]
        public async Task GetMyGradesAsync_WhenStudentExistsWithoutEnrollment_ShouldReturnEmptyList()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            context.Students.Add(new Student { Code = "EMPTY", Name = "No Class", Email = "empty@test.com", Status = (int)StudentStatus.Active });
            await context.SaveChangesAsync();

            var response = await CreateService(context).GetMyGradesAsync(new[] { "EMPTY" });

            response.Success.Should().BeTrue();
            response.Data.Should().BeEmpty();
        }

        [Fact]
        public async Task GetMyGradesAsync_ShouldMatchEmailLocalPartIgnoringCase()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            await SeedClassAsync(context);

            var response = await CreateService(context).GetMyGradesAsync(new[] { "STUDENT" });

            response.Success.Should().BeTrue();
            response.Data.Should().ContainSingle();
        }

        #endregion

    }
}
