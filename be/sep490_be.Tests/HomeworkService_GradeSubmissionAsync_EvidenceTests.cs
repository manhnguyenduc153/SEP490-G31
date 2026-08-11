using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using sep490_be.DTO.Homework;
using sep490_be.Models;
using sep490_be.Repositories.Common;
using sep490_be.Repositories.Implementations;
using sep490_be.Services.Implementations;
using sep490_be.Services.Interfaces;

namespace sep490_be.Tests.Services
{
    public class HomeworkService_GradeSubmissionAsync_EvidenceTests
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

        private HomeworkService CreateHomeworkService(ApplicationDbContext context)
        {
            var uow = new UnitOfWork<ApplicationDbContext>(context);
            var homeworkRepo = new HomeworkRepository(context, uow);
            var homeworkSubRepo = new HomeworkSubmissionRepository(context, uow);
            var notificationService = Mock.Of<INotificationService>();
            var logger = Mock.Of<ILogger<HomeworkService>>();

            return new HomeworkService(homeworkRepo, homeworkSubRepo, notificationService, logger);
        }

        [Fact]
        public async Task Normal_GradeSubmissionAsync_ValidScore_ShouldGradeSuccessfully()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int submissionId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var homework = new Homework { Title = "Test HW", TotalScore = 100, Status = 1 };
                context.Homeworks.Add(homework);
                await context.SaveChangesAsync();

                var submission = new HomeworkSubmission { HomeworkId = homework.Id, StudentId = 1, Content = "My work", Status = 1 };
                context.HomeworkSubmissions.Add(submission);
                await context.SaveChangesAsync();

                submissionId = submission.Id;
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateHomeworkService(context);
                var dto = new HomeworkSubmissionGradeDto { Score = 90, TeacherFeedback = "Good job" };
                
                var response = await service.GradeSubmissionAsync(submissionId, dto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data.Should().NotBeNull();
                response.Data!.Score.Should().Be(90);
                response.Data.TeacherFeedback.Should().Be("Good job");
                response.Data.Status.Should().Be(2);

                var dbSubmission = await context.HomeworkSubmissions.FindAsync(submissionId);
                dbSubmission!.Score.Should().Be(90);
                dbSubmission.Status.Should().Be(2);
            }
        }

        [Fact]
        public async Task Abnormal_GradeSubmissionAsync_ZeroSubmissionId_ShouldReturnBadRequest()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateHomeworkService(context);
                var dto = new HomeworkSubmissionGradeDto { Score = 10 };
                
                var response = await service.GradeSubmissionAsync(0, dto);

                // Assert
                response.Success.Should().BeFalse();
                response.Message.Should().Be("ERR_HOMEWORK_SUBMISSION_ID_REQUIRED");
            }
        }

        [Fact]
        public async Task Abnormal_GradeSubmissionAsync_SubmissionNotFound_ShouldReturn404()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateHomeworkService(context);
                var dto = new HomeworkSubmissionGradeDto { Score = 10 };
                
                var response = await service.GradeSubmissionAsync(999, dto);

                // Assert
                response.Success.Should().BeFalse();
                response.Message.Should().Be("ERR_HOMEWORK_SUBMISSION_NOT_FOUND");
            }
        }

        [Fact]
        public async Task Abnormal_GradeSubmissionAsync_HomeworkNotFound_ShouldReturn404()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int submissionId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                // Create submission mapping to a missing homework (HomeworkId = 999)
                var submission = new HomeworkSubmission { HomeworkId = 999, StudentId = 1, Content = "My work", Status = 1 };
                context.HomeworkSubmissions.Add(submission);
                await context.SaveChangesAsync();

                submissionId = submission.Id;
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateHomeworkService(context);
                var dto = new HomeworkSubmissionGradeDto { Score = 10 };
                
                var response = await service.GradeSubmissionAsync(submissionId, dto);

                // Assert
                response.Success.Should().BeFalse();
                response.Message.Should().Be("ERR_HOMEWORK_NOT_FOUND");
            }
        }

        [Fact]
        public async Task Abnormal_GradeSubmissionAsync_NegativeScore_ShouldReturnBadRequest()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int submissionId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var homework = new Homework { Title = "Test HW", TotalScore = 100, Status = 1 };
                context.Homeworks.Add(homework);
                await context.SaveChangesAsync();

                var submission = new HomeworkSubmission { HomeworkId = homework.Id, StudentId = 1, Content = "My work", Status = 1 };
                context.HomeworkSubmissions.Add(submission);
                await context.SaveChangesAsync();

                submissionId = submission.Id;
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateHomeworkService(context);
                var dto = new HomeworkSubmissionGradeDto { Score = -5 };
                
                var response = await service.GradeSubmissionAsync(submissionId, dto);

                // Assert
                response.Success.Should().BeFalse();
                response.Message.Should().Be("ERR_HOMEWORK_SCORE_INVALID");
            }
        }

        [Fact]
        public async Task Abnormal_GradeSubmissionAsync_ScoreExceedsTotal_ShouldReturnBadRequest()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int submissionId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var homework = new Homework { Title = "Test HW", TotalScore = 100, Status = 1 };
                context.Homeworks.Add(homework);
                await context.SaveChangesAsync();

                var submission = new HomeworkSubmission { HomeworkId = homework.Id, StudentId = 1, Content = "My work", Status = 1 };
                context.HomeworkSubmissions.Add(submission);
                await context.SaveChangesAsync();

                submissionId = submission.Id;
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateHomeworkService(context);
                var dto = new HomeworkSubmissionGradeDto { Score = 110 }; // Score (110) > TotalScore (100)
                
                var response = await service.GradeSubmissionAsync(submissionId, dto);

                // Assert
                response.Success.Should().BeFalse();
                response.Message.Should().Be("ERR_HOMEWORK_SCORE_EXCEEDS_TOTAL");
            }
        }
    }
}
