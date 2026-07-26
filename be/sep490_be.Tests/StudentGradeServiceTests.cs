using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
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

        private static StudentGradeService CreateService(ApplicationDbContext context)
        {
            var store = new Mock<IUserStore<IdentityUser>>();
            var userManager = new Mock<UserManager<IdentityUser>>(
                store.Object, null!, null!, Array.Empty<IUserValidator<IdentityUser>>(),
                Array.Empty<IPasswordValidator<IdentityUser>>(), null!, null!, null!, null!);
            return new StudentGradeService(context, userManager.Object);
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
            response.Data.Components.Select(x => x.Code).Should().Equal("attendance", "homework", "exam");
            response.Data.Components.Select(x => x.Weight).Should().Equal(30m, 30m, 40m);
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

            var schedule = new ClassSchedule { ClassId = classId, Status = 1 };
            context.ClassSchedules.Add(schedule);
            await context.SaveChangesAsync();
            context.Attendances.AddRange(
                new Attendance { ScheduleId = schedule.Id, StudentId = studentId, Status = 1 },
                new Attendance { ScheduleId = schedule.Id, StudentId = studentId, Status = 0 },
                new Attendance { ScheduleId = schedule.Id, StudentId = studentId, Status = -1 });

            var teacher = new Teacher { Code = "TC001", Name = "Teacher One", Email = "teacher@test.com" };
            context.Teachers.Add(teacher);
            await context.SaveChangesAsync();
            var homework1 = new Homework { ClassId = classId, TeacherId = teacher.Id, Title = "HW1", TotalScore = 10, Status = 1 };
            var homework2 = new Homework { ClassId = classId, TeacherId = teacher.Id, Title = "HW2", TotalScore = 20, Status = 1 };
            context.Homeworks.AddRange(homework1, homework2);

            var exam = new Exam { Code = "EX001", Name = "Exam One", Title = "Exam One", ClassId = classId, TotalScore = 20, Status = 1, Type = 1 };
            context.Exams.Add(exam);
            await context.SaveChangesAsync();
            context.HomeworkSubmissions.AddRange(
                new HomeworkSubmission { HomeworkId = homework1.Id, StudentId = studentId, SubmitTime = DateTime.UtcNow, Score = 8, Status = 2 },
                new HomeworkSubmission { HomeworkId = homework2.Id, StudentId = studentId, SubmitTime = DateTime.UtcNow, Score = 10, Status = 2 });
            context.ExamAttempts.Add(new ExamAttempt { ExamId = exam.Id, StudentId = studentId, StartTime = DateTime.UtcNow, Score = 15, Status = 2 });
            await context.SaveChangesAsync();

            var response = await service.GetMyGradesAsync(new[] { "student" });

            response.Success.Should().BeTrue();
            response.Data.Should().ContainSingle();
            var grade = response.Data!.Single();
            grade.Components.Single(x => x.ComponentCode == "attendance").RawScore.Should().Be(5);
            grade.Components.Single(x => x.ComponentCode == "homework").RawScore.Should().Be(6.5m);
            grade.Components.Single(x => x.ComponentCode == "exam").RawScore.Should().Be(7.5m);
            grade.AverageScore.Should().Be(6.5m);
            settings.Data!.Components.Should().HaveCount(3);
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
            var attendance = settings.Data!.Components.Single(x => x.Code == "attendance");
            await service.SaveOverridesAsync(classId, new StudentGradeOverridesSaveDto
            {
                Overrides = new List<StudentGradeOverrideSaveDto>
                {
                    new() { StudentClassId = studentClassId, GradeComponentId = attendance.Id, Score = 9 }
                }
            });

            var response = await service.GetMyGradesAsync(new[] { "ST001" });

            var component = response.Data!.Single().Components.Single(x => x.ComponentCode == "attendance");
            component.RawScore.Should().Be(0);
            component.Score.Should().Be(9);
            component.IsOverride.Should().BeTrue();
            response.Data!.Single().AverageScore.Should().Be(2.7m);
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
            var attendanceId = settings.Data!.Components.First(x => x.Code == "attendance").Id;
            var examId = settings.Data.Components.First(x => x.Code == "exam").Id;

            var response = await service.SaveOverridesAsync(classId, new StudentGradeOverridesSaveDto
            {
                Overrides = new List<StudentGradeOverrideSaveDto>
                {
                    new() { StudentClassId = studentClassId, GradeComponentId = attendanceId, Score = 12 },
                    new() { StudentClassId = studentClassId, GradeComponentId = examId, Score = -2 }
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
                    new() { Id = kept.Id, Code = kept.Code, Name = "Updated attendance", Weight = kept.Weight, SortOrder = 1, IsSystem = true }
                }
            });

            response.Success.Should().BeTrue();
            response.Data.Should().HaveCount(3);
            response.Data!.First(x => x.Id == kept.Id).Name.Should().Be("Updated attendance");
            (await context.GradeComponents.CountAsync(x => x.CourseId == course.Id)).Should().Be(3);
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
