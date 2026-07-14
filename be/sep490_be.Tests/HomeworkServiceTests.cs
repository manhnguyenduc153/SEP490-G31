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
                new HomeworkSubmissionRepository(context, unitOfWork));
        }

        private static async Task<(int classId, int teacherId, int studentId)> SeedActorsAsync(ApplicationDbContext context)
        {
            var course = new Course { Code = "C001", Name = "IELTS Foundation", Status = (int)GeneralStatus.Active };
            var classEntity = new ModelClass { Code = "CL001", Name = "Class One", Course = course, Status = (int)ClassStatus.Active };
            var teacher = new Teacher { Code = "TC001", Name = "Teacher One", Email = "teacher@test.com", Status = (int)TeacherStatus.Active };
            var student = new Student { Code = "ST001", Name = "Student One", Email = "student@test.com", Status = (int)StudentStatus.Active };

            context.AddRange(course, classEntity, teacher, student);
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
                ClassId = 999,
                TeacherId = 999,
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
        public async Task UpdateHomeworkAsync_WhenHomeworkDoesNotExist_ShouldReturnNotFound()
        {
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            using var context = new ApplicationDbContext(options, mockHttp.Object);

            var response = await CreateService(context).UpdateHomeworkAsync(9999, new HomeworkSaveDto { Title = "Missing" });

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
            response.Message.Should().Be("Không tìm thấy bài tập");
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
            response.Message.Should().Be("Khong xac dinh duoc sinh vien");
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
            response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
            response.Message.Should().Be("Bài tập không khả dụng hoặc đã đóng");
        }

        [Theory]
        [InlineData(false, 1)]
        [InlineData(true, 3)]
        public async Task SubmitHomeworkAsync_ShouldSetSubmissionStatusFromDueDate(bool isLate, int expectedStatus)
        {
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            using var context = new ApplicationDbContext(options, mockHttp.Object);
            var (classId, teacherId, studentId) = await SeedActorsAsync(context);
            var dueDate = isLate ? DateTime.UtcNow.AddHours(-1) : DateTime.UtcNow.AddHours(1);
            var homework = await SeedHomeworkAsync(context, classId, teacherId, dueDate);

            var response = await CreateService(context).SubmitHomeworkAsync(new HomeworkSubmissionSaveDto
            {
                HomeworkId = homework.Id,
                StudentId = studentId,
                Content = "My answer"
            });

            response.Success.Should().BeTrue();
            response.Data!.Status.Should().Be(expectedStatus);
            (await context.HomeworkSubmissions.SingleAsync()).Content.Should().Be("My answer");
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

        [Fact]
        public async Task GradeSubmissionAsync_WhenSubmissionDoesNotExist_ShouldReturnNotFound()
        {
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            using var context = new ApplicationDbContext(options, mockHttp.Object);

            var response = await CreateService(context).GradeSubmissionAsync(9999, new HomeworkSubmissionGradeDto { Score = 5 });

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
            response.Message.Should().Be("Không tìm thấy bài nộp");
        }
    }
}
