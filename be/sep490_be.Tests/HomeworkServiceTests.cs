using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using sep490_be.DTO.Homework;
using sep490_be.Enums;
using sep490_be.Models;
using sep490_be.Repositories.Common;
using sep490_be.Repositories.Implementations;
using sep490_be.Services.Implementations;
using ModelClass = sep490_be.Models.Class;

namespace sep490_be.Tests.Services
{
    public class HomeworkServiceTests
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

        private static HomeworkService CreateService(ApplicationDbContext context)
        {
            var unitOfWork = new UnitOfWork<ApplicationDbContext>(context);
            return new HomeworkService(
                new HomeworkRepository(context, unitOfWork),
                new HomeworkSubmissionRepository(context, unitOfWork),
                context);
        }

        private static async Task<(int classId, int teacherId, int studentId)> SeedActorsAsync(ApplicationDbContext context)
        {
            var course = new Course { Code = "C001", Name = "IELTS Foundation", Status = (int)GeneralStatus.Active };
            var teacher = new Teacher { Code = "TC001", Name = "Teacher One", Email = "teacher@test.com", Status = (int)TeacherStatus.Active };
            var classEntity = new ModelClass { Code = "CL001", Name = "Class One", Course = course, Teacher = teacher, Status = (int)ClassStatus.Active };
            var student = new Student { Code = "ST001", Name = "Student One", Email = "student@test.com", Status = (int)StudentStatus.Active };
            var enrollment = new StudentClass { Student = student, Class = classEntity, Status = 1, EnrollDate = DateTime.UtcNow };

            context.AddRange(course, classEntity, teacher, student, enrollment);
            await context.SaveChangesAsync();
            return (classEntity.Id, teacher.Id, student.Id);
        }

        private static async Task<Homework> SeedHomeworkAsync(
            ApplicationDbContext context,
            int classId,
            int teacherId,
            DateTime? dueDate = null,
            int status = 1,
            decimal totalScore = 10)
        {
            var homework = new Homework
            {
                ClassId = classId,
                TeacherId = teacherId,
                Title = "Writing task",
                Description = "Write an essay",
                DueDate = dueDate,
                TotalScore = totalScore,
                Status = status
            };
            context.Homeworks.Add(homework);
            await context.SaveChangesAsync();
            return homework;
        }

        #region Normal Test Cases (Kiểm thử giá trị thông thường)

        [Fact]
        public async Task GetHomeworkByClassAsync_ShouldReturnOnlyActiveHomeworkInNewestFirstOrder()
        {
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            using var context = new ApplicationDbContext(options, mockHttp.Object);
            var (classId, teacherId, _) = await SeedActorsAsync(context);
            var older = await SeedHomeworkAsync(context, classId, teacherId);
            older.Title = "Older";
            older.CreatedAt = DateTime.UtcNow.AddDays(-2);
            var newer = await SeedHomeworkAsync(context, classId, teacherId);
            newer.Title = "Newer";
            newer.CreatedAt = DateTime.UtcNow.AddDays(-1);
            var deleted = await SeedHomeworkAsync(context, classId, teacherId);
            deleted.Title = "Deleted";
            deleted.IsDeleted = true;
            await context.SaveChangesAsync();

            var response = await CreateService(context).GetHomeworkByClassAsync(classId);

            response.Success.Should().BeTrue();
            response.Data!.Select(x => x.Title).Should().Equal("Newer", "Older");
            response.Data!.Should().OnlyContain(x => x.ClassName == "Class One");
        }
        [Fact]
        public async Task CreateHomeworkAsync_WithValidDto_ShouldPersistAndMapHomework()
        {
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            using var context = new ApplicationDbContext(options, mockHttp.Object);
            var (classId, teacherId, _) = await SeedActorsAsync(context);
            var dueDate = DateTime.UtcNow.AddDays(3);

            var response = await CreateService(context).CreateHomeworkAsync(new HomeworkSaveDto
            {
                ClassId = classId,
                TeacherId = teacherId,
                Title = "Listening practice",
                Description = "Complete section 1",
                AttachmentUrls = new List<string> { "https://test/file.mp3" },
                Skill = "Listening",
                DueDate = dueDate,
                TotalScore = 20,
                Status = 1
            });

            response.Success.Should().BeTrue();
            response.Data!.Id.Should().BeGreaterThan(0);
            response.Data.Title.Should().Be("Listening practice");
            var saved = await context.Homeworks.SingleAsync();
            saved.TeacherId.Should().Be(teacherId);
            saved.AttachmentUrls.Should().ContainSingle("https://test/file.mp3");
        }
        [Fact]
        public async Task UpdateHomeworkAsync_WhenHomeworkExists_ShouldUpdateEditableFieldsOnly()
        {
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            using var context = new ApplicationDbContext(options, mockHttp.Object);
            var (classId, teacherId, _) = await SeedActorsAsync(context);
            var homework = await SeedHomeworkAsync(context, classId, teacherId);

            var response = await CreateService(context).UpdateHomeworkAsync(homework.Id, new HomeworkSaveDto
            {
                ClassId = classId,
                TeacherId = teacherId,
                Title = "Updated task",
                Description = "Updated description",
                Skill = "Reading",
                DueDate = DateTime.UtcNow.AddDays(5),
                TotalScore = 25,
                Status = 0
            });

            response.Success.Should().BeTrue();
            response.Data!.Title.Should().Be("Updated task");
            homework.ClassId.Should().Be(classId);
            homework.TeacherId.Should().Be(teacherId);
            homework.TotalScore.Should().Be(25);
            homework.Status.Should().Be(0);
        }
        [Fact]
        public async Task DeleteHomeworkAsync_WhenHomeworkExists_ShouldSoftDeleteHomework()
        {
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            using var context = new ApplicationDbContext(options, mockHttp.Object);
            var (classId, teacherId, _) = await SeedActorsAsync(context);
            var homework = await SeedHomeworkAsync(context, classId, teacherId);

            var response = await CreateService(context).DeleteHomeworkAsync(homework.Id);

            response.Success.Should().BeTrue();
            response.Data.Should().BeTrue();
            var deleted = await context.Homeworks.SingleAsync(x => x.Id == homework.Id);
            deleted.IsDeleted.Should().BeTrue();
            deleted.DeletedAt.Should().NotBeNull();
            var listResponse = await CreateService(context).GetHomeworkByClassAsync(classId);
            listResponse.Data.Should().BeEmpty();
        }
        [Fact]
        public async Task SubmitHomeworkAsync_WhenResubmitting_ShouldUpdateExistingAndClearGrade()
        {
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            using var context = new ApplicationDbContext(options, mockHttp.Object);
            var (classId, teacherId, studentId) = await SeedActorsAsync(context);
            var homework = await SeedHomeworkAsync(context, classId, teacherId, DateTime.UtcNow.AddHours(1));
            var existing = new HomeworkSubmission
            {
                HomeworkId = homework.Id,
                StudentId = studentId,
                Content = "Old answer",
                SubmitTime = DateTime.UtcNow.AddDays(-1),
                Score = 8,
                TeacherFeedback = "Old feedback",
                Status = 2
            };
            context.HomeworkSubmissions.Add(existing);
            await context.SaveChangesAsync();

            var response = await CreateService(context).SubmitHomeworkAsync(new HomeworkSubmissionSaveDto
            {
                HomeworkId = homework.Id,
                StudentId = studentId,
                Content = "Revised answer"
            });

            response.Success.Should().BeTrue();
            response.Data!.Id.Should().Be(existing.Id);
            response.Data.Content.Should().Be("Revised answer");
            response.Data.Score.Should().BeNull();
            response.Data.TeacherFeedback.Should().BeNull();
            (await context.HomeworkSubmissions.CountAsync()).Should().Be(1);
        }
        [Fact]
        public async Task GetSubmissionsByHomeworkAsync_ShouldReturnStudentDetailsAndExcludeDeletedRows()
        {
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            using var context = new ApplicationDbContext(options, mockHttp.Object);
            var (classId, teacherId, studentId) = await SeedActorsAsync(context);
            var homework = await SeedHomeworkAsync(context, classId, teacherId);
            var visible = new HomeworkSubmission { HomeworkId = homework.Id, StudentId = studentId, SubmitTime = DateTime.UtcNow, Content = "Visible" };
            var deleted = new HomeworkSubmission { HomeworkId = homework.Id, StudentId = studentId, SubmitTime = DateTime.UtcNow.AddMinutes(-1), Content = "Deleted" };
            context.HomeworkSubmissions.AddRange(visible, deleted);
            await context.SaveChangesAsync();
            deleted.IsDeleted = true;
            await context.SaveChangesAsync();

            var response = await CreateService(context).GetSubmissionsByHomeworkAsync(homework.Id);

            response.Success.Should().BeTrue();
            response.Data.Should().ContainSingle();
            var submission = response.Data!.Single();
            submission.Content.Should().Be("Visible");
            submission.StudentName.Should().Be("Student One");
            submission.StudentCode.Should().Be("ST001");
        }
        [Fact]
        public async Task GradeSubmissionAsync_WithValidScore_ShouldPersistGradeAndFeedback()
        {
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            using var context = new ApplicationDbContext(options, mockHttp.Object);
            var (classId, teacherId, studentId) = await SeedActorsAsync(context);
            var homework = await SeedHomeworkAsync(context, classId, teacherId, totalScore: 10);
            var submission = new HomeworkSubmission { HomeworkId = homework.Id, StudentId = studentId, SubmitTime = DateTime.UtcNow, Status = 1 };
            context.HomeworkSubmissions.Add(submission);
            await context.SaveChangesAsync();

            var response = await CreateService(context).GradeSubmissionAsync(submission.Id, new HomeworkSubmissionGradeDto
            {
                Score = 8.5m,
                TeacherFeedback = "Good work"
            });

            response.Success.Should().BeTrue();
            response.Data!.Score.Should().Be(8.5m);
            response.Data.Status.Should().Be(2);
            submission.TeacherFeedback.Should().Be("Good work");
        }

        #endregion

        #region Boundary Test Cases (Kiểm thử giá trị biên)

        [Fact]
        public async Task SubmitHomeworkAsync_BeforeDueDate_ShouldCreateSubmittedSubmission()
        {
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            using var context = new ApplicationDbContext(options, mockHttp.Object);
            var (classId, teacherId, studentId) = await SeedActorsAsync(context);
            var homework = await SeedHomeworkAsync(context, classId, teacherId, DateTime.UtcNow.AddHours(1));

            var response = await CreateService(context).SubmitHomeworkAsync(new HomeworkSubmissionSaveDto
            {
                HomeworkId = homework.Id,
                StudentId = studentId,
                Content = "My answer"
            });

            response.Success.Should().BeTrue();
            response.Data!.Status.Should().Be(1);
            (await context.HomeworkSubmissions.SingleAsync()).Content.Should().Be("My answer");
        }

        [Fact]
        public async Task SubmitHomeworkAsync_AfterDueDate_ShouldReturnSubmissionClosedAndNotPersist()
        {
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            using var context = new ApplicationDbContext(options, mockHttp.Object);
            var (classId, teacherId, studentId) = await SeedActorsAsync(context);
            var homework = await SeedHomeworkAsync(context, classId, teacherId, DateTime.UtcNow.AddHours(-1));

            var response = await CreateService(context).SubmitHomeworkAsync(new HomeworkSubmissionSaveDto
            {
                HomeworkId = homework.Id,
                StudentId = studentId,
                Content = "My late answer"
            });

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
            response.Message.Should().Be("ERR_SUBMISSION_CLOSED");
            context.HomeworkSubmissions.Should().BeEmpty();
        }

        [Fact]
        public async Task GradeSubmissionAsync_WhenScoreExceedsTotal_ShouldReturnBadRequestWithoutChangingSubmission()
        {
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            using var context = new ApplicationDbContext(options, mockHttp.Object);
            var (classId, teacherId, studentId) = await SeedActorsAsync(context);
            var homework = await SeedHomeworkAsync(context, classId, teacherId, totalScore: 10);
            var submission = new HomeworkSubmission { HomeworkId = homework.Id, StudentId = studentId, SubmitTime = DateTime.UtcNow, Status = 1 };
            context.HomeworkSubmissions.Add(submission);
            await context.SaveChangesAsync();

            var response = await CreateService(context).GradeSubmissionAsync(submission.Id, new HomeworkSubmissionGradeDto
            {
                Score = 11,
                TeacherFeedback = "Too high"
            });

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
            submission.Score.Should().BeNull();
            submission.Status.Should().Be(1);
        }

        #endregion

        #region Abnormal Test Cases (Kiểm thử giá trị bất thường / Lỗi)

        [Fact]
        public async Task UpdateHomeworkAsync_WhenHomeworkDoesNotExist_ShouldReturnNotFound()
        {
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            using var context = new ApplicationDbContext(options, mockHttp.Object);

            var response = await CreateService(context).UpdateHomeworkAsync(9999, new HomeworkSaveDto { Title = "Missing" });

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
            response.Message.Should().Be("ERR_HOMEWORK_NOT_FOUND");
        }
        [Fact]
        public async Task SubmitHomeworkAsync_WithoutStudentId_ShouldReturnBadRequest()
        {
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            using var context = new ApplicationDbContext(options, mockHttp.Object);

            var response = await CreateService(context).SubmitHomeworkAsync(new HomeworkSubmissionSaveDto
            {
                HomeworkId = 1,
                StudentId = null
            });

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
            response.Message.Should().Be("ERR_HOMEWORK_STUDENT_REQUIRED");
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task SubmitHomeworkAsync_WhenHomeworkIsUnavailable_ShouldReturnBadRequest(int scenario)
        {
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            using var context = new ApplicationDbContext(options, mockHttp.Object);
            var (classId, teacherId, studentId) = await SeedActorsAsync(context);
            var homeworkId = 9999;
            if (scenario == 1)
            {
                homeworkId = (await SeedHomeworkAsync(context, classId, teacherId, status: 0)).Id;
            }

            var response = await CreateService(context).SubmitHomeworkAsync(new HomeworkSubmissionSaveDto
            {
                HomeworkId = homeworkId,
                StudentId = studentId
            });

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(scenario == 0
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest);
            response.Message.Should().Be(scenario == 0 ? "ERR_HOMEWORK_NOT_FOUND" : "ERR_HOMEWORK_CLOSED");
        }
        [Fact]
        public async Task GradeSubmissionAsync_WhenSubmissionDoesNotExist_ShouldReturnNotFound()
        {
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            using var context = new ApplicationDbContext(options, mockHttp.Object);

            var response = await CreateService(context).GradeSubmissionAsync(9999, new HomeworkSubmissionGradeDto { Score = 5 });

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
            response.Message.Should().Be("ERR_HOMEWORK_SUBMISSION_NOT_FOUND");
        }

        [Fact]
        public async Task GetHomeworkByClassAsync_WhenClassDoesNotExist_ShouldReturnNotFound()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var response = await CreateService(context).GetHomeworkByClassAsync(123);
            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
            response.Message.Should().Be("ERR_HOMEWORK_CLASS_NOT_FOUND");
        }

        [Fact]
        public async Task GetHomeworkByClassAsync_ShouldNotReturnHomeworkFromAnotherClass()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var (classId, teacherId, _) = await SeedActorsAsync(context);
            var otherClass = new ModelClass { Code = "OTHER", Name = "Other", Status = (int)ClassStatus.Active };
            context.Classes.Add(otherClass);
            await context.SaveChangesAsync();
            await SeedHomeworkAsync(context, classId, teacherId);
            await SeedHomeworkAsync(context, otherClass.Id, teacherId);
            var response = await CreateService(context).GetHomeworkByClassAsync(classId);
            response.Data.Should().ContainSingle(x => x.ClassId == classId);
        }

        [Fact]
        public async Task CreateHomeworkAsync_WithAttachmentsAndSkill_ShouldRoundTripAllFields()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var (classId, teacherId, _) = await SeedActorsAsync(context);
            var dueDate = DateTime.UtcNow.AddDays(3);
            var response = await CreateService(context).CreateHomeworkAsync(new HomeworkSaveDto
            {
                ClassId = classId, TeacherId = teacherId, Title = "Listening files",
                Description = "Listen and answer", AttachmentUrls = new List<string> { "/a.mp3", "/b.pdf" },
                Skill = "Listening", DueDate = dueDate, TotalScore = 20, Status = 1
            });
            response.Success.Should().BeTrue();
            response.Data!.AttachmentUrls.Should().Equal("/a.mp3", "/b.pdf");
            response.Data.Skill.Should().Be("Listening");
            response.Data.DueDate.Should().Be(dueDate);
            response.Data.TotalScore.Should().Be(20);
        }

        [Fact]
        public async Task UpdateHomeworkAsync_WhenHomeworkWasDeleted_ShouldReturnNotFound()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var (classId, teacherId, _) = await SeedActorsAsync(context);
            var homework = await SeedHomeworkAsync(context, classId, teacherId);
            homework.IsDeleted = true;
            await context.SaveChangesAsync();
            var response = await CreateService(context).UpdateHomeworkAsync(homework.Id, new HomeworkSaveDto { Title = "Updated" });
            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        }

        [Fact]
        public async Task DeleteHomeworkAsync_WhenHomeworkDoesNotExist_ShouldReturnNotFound()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var response = await CreateService(context).DeleteHomeworkAsync(9999);
            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        }

        [Fact]
        public async Task DeleteHomeworkAsync_WhenCalledTwice_ShouldReturnNotFoundOnSecondCall()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var (classId, teacherId, _) = await SeedActorsAsync(context);
            var homework = await SeedHomeworkAsync(context, classId, teacherId);
            var service = CreateService(context);
            (await service.DeleteHomeworkAsync(homework.Id)).Success.Should().BeTrue();
            var second = await service.DeleteHomeworkAsync(homework.Id);
            second.Success.Should().BeFalse();
            second.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        }

        [Fact]
        public async Task GetSubmissionsByHomeworkAsync_WhenHomeworkDoesNotExist_ShouldReturnNotFound()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var response = await CreateService(context).GetSubmissionsByHomeworkAsync(123);
            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        }

        [Fact]
        public async Task SubmitHomeworkAsync_WithZeroStudentId_ShouldReturnBadRequest()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var response = await CreateService(context).SubmitHomeworkAsync(new HomeworkSubmissionSaveDto { HomeworkId = 1, StudentId = 0 });
            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        }

        [Fact]
        public async Task SubmitHomeworkAsync_WithContentAndAttachments_ShouldPersistSubmission()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var (classId, teacherId, studentId) = await SeedActorsAsync(context);
            var homework = await SeedHomeworkAsync(context, classId, teacherId, DateTime.UtcNow.AddDays(1));
            var response = await CreateService(context).SubmitHomeworkAsync(new HomeworkSubmissionSaveDto
            {
                HomeworkId = homework.Id, StudentId = studentId, Content = "My answer",
                AttachmentUrls = new List<string> { "/answer.docx", "/audio.mp3" }
            });
            response.Success.Should().BeTrue();
            response.Data!.Content.Should().Be("My answer");
            response.Data.AttachmentUrls.Should().HaveCount(2);
            (await context.HomeworkSubmissions.SingleAsync()).AttachmentUrls.Should().Equal("/answer.docx", "/audio.mp3");
        }

        [Fact]
        public async Task GradeSubmissionAsync_WithScoreExactlyAtMaximum_ShouldSucceed()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var (classId, teacherId, studentId) = await SeedActorsAsync(context);
            var homework = await SeedHomeworkAsync(context, classId, teacherId, totalScore: 10);
            var submission = new HomeworkSubmission { HomeworkId = homework.Id, StudentId = studentId, SubmitTime = DateTime.UtcNow, Status = 1 };
            context.HomeworkSubmissions.Add(submission);
            await context.SaveChangesAsync();
            var response = await CreateService(context).GradeSubmissionAsync(submission.Id, new HomeworkSubmissionGradeDto { Score = 10, TeacherFeedback = "Perfect" });
            response.Success.Should().BeTrue();
            response.Data!.Score.Should().Be(10);
            response.Data.Status.Should().Be(2);
            response.Data.TeacherFeedback.Should().Be("Perfect");
        }

        [Fact]
        public async Task CreateHomeworkAsync_WhenClassDoesNotExist_ShouldReturnNotFoundAndNotPersist()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var (_, teacherId, _) = await SeedActorsAsync(context);

            var response = await CreateService(context).CreateHomeworkAsync(new HomeworkSaveDto
            {
                ClassId = 9999, TeacherId = teacherId, Title = "Invalid class", TotalScore = 10, Status = 1
            });

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
            response.Message.Should().Be("ERR_HOMEWORK_CLASS_NOT_FOUND");
            context.Homeworks.Should().BeEmpty();
        }

        [Fact]
        public async Task CreateHomeworkAsync_WhenTeacherDoesNotExist_ShouldReturnNotFoundAndNotPersist()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var (classId, _, _) = await SeedActorsAsync(context);

            var response = await CreateService(context).CreateHomeworkAsync(new HomeworkSaveDto
            {
                ClassId = classId, TeacherId = 9999, Title = "Invalid teacher", TotalScore = 10, Status = 1
            });

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
            response.Message.Should().Be("ERR_HOMEWORK_TEACHER_NOT_FOUND");
            context.Homeworks.Should().BeEmpty();
        }

        [Fact]
        public async Task CreateHomeworkAsync_WhenTeacherIsNotAssignedToClass_ShouldReturnBadRequest()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var (classId, _, _) = await SeedActorsAsync(context);
            var otherTeacher = new Teacher { Code = "TC002", Name = "Other Teacher", Status = (int)TeacherStatus.Active };
            context.Teachers.Add(otherTeacher);
            await context.SaveChangesAsync();

            var response = await CreateService(context).CreateHomeworkAsync(new HomeworkSaveDto
            {
                ClassId = classId, TeacherId = otherTeacher.Id, Title = "Wrong teacher", TotalScore = 10, Status = 1
            });

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
            response.Message.Should().Be("ERR_HOMEWORK_TEACHER_NOT_ASSIGNED_TO_CLASS");
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task CreateHomeworkAsync_WhenTitleIsBlank_ShouldReturnBadRequest(string title)
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var (classId, teacherId, _) = await SeedActorsAsync(context);

            var response = await CreateService(context).CreateHomeworkAsync(new HomeworkSaveDto
            {
                ClassId = classId, TeacherId = teacherId, Title = title, TotalScore = 10, Status = 1
            });

            response.Success.Should().BeFalse();
            response.Message.Should().Be("ERR_HOMEWORK_TITLE_REQUIRED");
        }

        [Fact]
        public async Task CreateHomeworkAsync_WhenDueDateIsInThePast_ShouldReturnBadRequestAndNotPersist()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var (classId, teacherId, _) = await SeedActorsAsync(context);

            var response = await CreateService(context).CreateHomeworkAsync(new HomeworkSaveDto
            {
                ClassId = classId,
                TeacherId = teacherId,
                Title = "Expired homework",
                DueDate = DateTime.UtcNow.AddMinutes(-1),
                TotalScore = 10,
                Status = 1
            });

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
            response.Message.Should().Be("ERR_DUE_DATE_INVALID");
            context.Homeworks.Should().BeEmpty();
        }

        [Fact]
        public async Task UpdateHomeworkAsync_WhenReferencesAreInvalid_ShouldNotChangeHomework()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var (classId, teacherId, _) = await SeedActorsAsync(context);
            var homework = await SeedHomeworkAsync(context, classId, teacherId);

            var response = await CreateService(context).UpdateHomeworkAsync(homework.Id, new HomeworkSaveDto
            {
                ClassId = 9999, TeacherId = teacherId, Title = "Should not save", TotalScore = 10, Status = 1
            });

            response.Success.Should().BeFalse();
            homework.Title.Should().Be("Writing task");
            homework.ClassId.Should().Be(classId);
        }

        [Fact]
        public async Task GetHomeworkByClassAsync_WithNonPositiveId_ShouldReturnBadRequest()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);

            var response = await CreateService(context).GetHomeworkByClassAsync(0);

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
            response.Message.Should().Be("ERR_HOMEWORK_CLASS_REQUIRED");
        }

        [Fact]
        public async Task GetSubmissionsByHomeworkAsync_WhenHomeworkExistsWithoutSubmissions_ShouldReturnEmpty()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var (classId, teacherId, _) = await SeedActorsAsync(context);
            var homework = await SeedHomeworkAsync(context, classId, teacherId);

            var response = await CreateService(context).GetSubmissionsByHomeworkAsync(homework.Id);

            response.Success.Should().BeTrue();
            response.Data.Should().BeEmpty();
        }

        [Fact]
        public async Task SubmitHomeworkAsync_WhenStudentDoesNotExist_ShouldReturnNotFound()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var (classId, teacherId, _) = await SeedActorsAsync(context);
            var homework = await SeedHomeworkAsync(context, classId, teacherId);

            var response = await CreateService(context).SubmitHomeworkAsync(new HomeworkSubmissionSaveDto
            {
                HomeworkId = homework.Id, StudentId = 9999, Content = "Answer"
            });

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
            response.Message.Should().Be("ERR_HOMEWORK_STUDENT_NOT_FOUND");
        }

        [Fact]
        public async Task SubmitHomeworkAsync_WhenStudentIsNotEnrolled_ShouldReturnForbidden()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var (classId, teacherId, _) = await SeedActorsAsync(context);
            var homework = await SeedHomeworkAsync(context, classId, teacherId);
            var otherStudent = new Student { Code = "ST002", Name = "Other Student", Status = (int)StudentStatus.Active };
            context.Students.Add(otherStudent);
            await context.SaveChangesAsync();

            var response = await CreateService(context).SubmitHomeworkAsync(new HomeworkSubmissionSaveDto
            {
                HomeworkId = homework.Id, StudentId = otherStudent.Id, Content = "Answer"
            });

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
            response.Message.Should().Be("ERR_HOMEWORK_STUDENT_NOT_ENROLLED");
        }

        [Fact]
        public async Task SubmitHomeworkAsync_WithoutContentOrFiles_ShouldReturnBadRequest()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var (classId, teacherId, studentId) = await SeedActorsAsync(context);
            var homework = await SeedHomeworkAsync(context, classId, teacherId);

            var response = await CreateService(context).SubmitHomeworkAsync(new HomeworkSubmissionSaveDto
            {
                HomeworkId = homework.Id, StudentId = studentId, Content = " ", AttachmentUrls = new List<string>()
            });

            response.Success.Should().BeFalse();
            response.Message.Should().Be("ERR_HOMEWORK_SUBMISSION_CONTENT_REQUIRED");
        }

        [Fact]
        public async Task GradeSubmissionAsync_WithNegativeScore_ShouldReturnBadRequest()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var (classId, teacherId, studentId) = await SeedActorsAsync(context);
            var homework = await SeedHomeworkAsync(context, classId, teacherId);
            var submission = new HomeworkSubmission { HomeworkId = homework.Id, StudentId = studentId, SubmitTime = DateTime.UtcNow };
            context.HomeworkSubmissions.Add(submission);
            await context.SaveChangesAsync();

            var response = await CreateService(context).GradeSubmissionAsync(
                submission.Id, new HomeworkSubmissionGradeDto { Score = -1 });

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
            response.Message.Should().Be("ERR_HOMEWORK_SCORE_INVALID");
        }

        #endregion

    }
}
