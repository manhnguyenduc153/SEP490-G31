using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using sep490_be.Enums;
using sep490_be.Models;
using sep490_be.Repositories.Common;
using sep490_be.Repositories.Implementations;
using sep490_be.Services.Implementations;
using sep490_be.Services.Interfaces;

namespace sep490_be.Tests.Services
{
    public class HomeworkService_DeleteHomeworkAsync_EvidenceTests
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
        public async Task Normal_DeleteHomeworkAsync_ValidInput_ShouldSoftDeleteAndReturnSuccess()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int homeworkId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var homework = new Homework
                {
                    Title = "Homework to delete",
                    Status = 1,
                    IsDeleted = false
                };
                context.Homeworks.Add(homework);
                await context.SaveChangesAsync();
                
                homeworkId = homework.Id;
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateHomeworkService(context);

                var response = await service.DeleteHomeworkAsync(homeworkId);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data.Should().BeTrue();
                
                var deleted = await context.Homeworks.FindAsync(homeworkId);
                deleted!.IsDeleted.Should().BeTrue();
            }
        }

        [Fact]
        public async Task Abnormal_DeleteHomeworkAsync_ZeroId_ShouldReturnBadRequest()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateHomeworkService(context);

                var response = await service.DeleteHomeworkAsync(0);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.Message.Should().Be("ERR_HOMEWORK_ID_REQUIRED");
            }
        }

        [Fact]
        public async Task Abnormal_DeleteHomeworkAsync_NotFound_ShouldReturn404()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateHomeworkService(context);

                var response = await service.DeleteHomeworkAsync(999);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.Message.Should().Be("ERR_HOMEWORK_NOT_FOUND");
            }
        }

        [Fact]
        public async Task Abnormal_DeleteHomeworkAsync_AlreadyDeleted_ShouldReturn404()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int homeworkId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var homework = new Homework
                {
                    Title = "Already Deleted Homework",
                    IsDeleted = true
                };
                context.Homeworks.Add(homework);
                await context.SaveChangesAsync();
                
                homeworkId = homework.Id;
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateHomeworkService(context);

                var response = await service.DeleteHomeworkAsync(homeworkId);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.Message.Should().Be("ERR_HOMEWORK_NOT_FOUND");
            }
        }
    }
}
