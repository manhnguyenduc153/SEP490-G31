using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using sep490_be.DTO.Homework;
using sep490_be.Enums;
using sep490_be.Models;
using sep490_be.Repositories.Common;
using sep490_be.Repositories.Implementations;
using sep490_be.Services.Implementations;
using sep490_be.Services.Interfaces;
using ModelClass = sep490_be.Models.Class;

namespace sep490_be.Tests.Services
{
    public class HomeworkService_UpdateHomeworkAsync_EvidenceTests
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
        public async Task Normal_UpdateHomeworkAsync_ValidInput_ShouldUpdateAndReturnSuccess()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int classId, teacherId, homeworkId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var course = new Course { Code = "C001", Name = "Course 1", Status = (int)GeneralStatus.Active };
                var teacher = new Teacher { Code = "TC001", Name = "Teacher 1", Status = (int)TeacherStatus.Active };
                var classEntity = new ModelClass { Code = "CL001", Name = "Class 1", Course = course, Teacher = teacher, Status = (int)ClassStatus.Active };
                context.AddRange(course, teacher, classEntity);
                await context.SaveChangesAsync();

                classId = classEntity.Id;
                teacherId = teacher.Id;

                var homework = new Homework
                {
                    ClassId = classId,
                    TeacherId = teacherId,
                    Title = "Old Title",
                    Description = "Old Description",
                    Status = 0
                };
                context.Homeworks.Add(homework);
                await context.SaveChangesAsync();
                
                homeworkId = homework.Id;
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateHomeworkService(context);

                var saveDto = new HomeworkSaveDto
                {
                    ClassId = classId,
                    TeacherId = teacherId,
                    Title = "New Title",
                    Description = "New Description",
                    Skill = "Speaking",
                    TotalScore = 100,
                    Status = 1
                };

                var response = await service.UpdateHomeworkAsync(homeworkId, saveDto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data.Should().NotBeNull();
                response.Data!.Title.Should().Be("New Title");
                
                var updated = await context.Homeworks.FindAsync(homeworkId);
                updated!.Title.Should().Be("New Title");
                updated.Status.Should().Be(1);
            }
        }

        [Fact]
        public async Task Abnormal_UpdateHomeworkAsync_ZeroId_ShouldReturnBadRequest()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateHomeworkService(context);

                var response = await service.UpdateHomeworkAsync(0, new HomeworkSaveDto { Title = "Test" });

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.Message.Should().Be("ERR_HOMEWORK_ID_REQUIRED");
            }
        }

        [Fact]
        public async Task Abnormal_UpdateHomeworkAsync_NotFound_ShouldReturn404()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateHomeworkService(context);

                var response = await service.UpdateHomeworkAsync(999, new HomeworkSaveDto { Title = "Test" });

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.Message.Should().Be("ERR_HOMEWORK_NOT_FOUND");
            }
        }

        [Fact]
        public async Task Abnormal_UpdateHomeworkAsync_DeletedHomework_ShouldReturn404()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int homeworkId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var homework = new Homework
                {
                    Title = "Deleted Homework",
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

                var response = await service.UpdateHomeworkAsync(homeworkId, new HomeworkSaveDto { Title = "Test" });

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.Message.Should().Be("ERR_HOMEWORK_NOT_FOUND");
            }
        }
    }
}
