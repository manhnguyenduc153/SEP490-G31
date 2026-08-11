using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
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
    public class HomeworkService_SubmitHomeworkAsync_EvidenceTests
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

        private async Task<(int classId, int studentId)> SeedBaseDataAsync(ApplicationDbContext context, string username, string email)
        {
            var user = new IdentityUser { UserName = username, Email = email };
            context.Users.Add(user);
            
            var course = new Course { Code = "C001", Name = "Course 1", Status = (int)GeneralStatus.Active };
            var teacher = new Teacher { Code = "TC001", Name = "Teacher 1", Email = "teacher@test.com", Status = (int)TeacherStatus.Active };
            var classEntity = new ModelClass { Code = "CL001", Name = "Class 1", Course = course, Teacher = teacher, Status = (int)ClassStatus.Active };
            var student = new Student { Code = "ST001", Name = "Student 1", Email = email, Status = (int)StudentStatus.Active };
            var enrollment = new StudentClass { Student = student, Class = classEntity, Status = (int)StudentClassStatus.Enrolled };

            context.AddRange(course, teacher, classEntity, student, enrollment);
            await context.SaveChangesAsync();
            return (classEntity.Id, student.Id);
        }

        [Fact]
        public async Task Normal_SubmitHomeworkAsync_NewSubmission_ShouldSucceed()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            string testUser = "student_1";
            string testEmail = "student1@test.com";
            int homeworkId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (classId, _) = await SeedBaseDataAsync(context, testUser, testEmail);
                
                var homework = new Homework
                {
                    ClassId = classId,
                    Title = "Homework 1",
                    Status = 1,
                    DueDate = DateTime.UtcNow.AddDays(1)
                };
                context.Homeworks.Add(homework);
                await context.SaveChangesAsync();
                homeworkId = homework.Id;
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateHomeworkService(context);

                var dto = new HomeworkSubmissionSaveDto
                {
                    HomeworkId = homeworkId,
                    Content = "My answers"
                };

                var response = await service.SubmitHomeworkAsync(dto, testUser);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data.Should().NotBeNull();
                response.Data!.Content.Should().Be("My answers");
                response.Data.Status.Should().Be(1);
            }
        }

        [Fact]
        public async Task Normal_SubmitHomeworkAsync_Resubmit_ShouldUpdateExistingAndClearScore()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            string testUser = "student_2";
            string testEmail = "student2@test.com";
            int homeworkId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (classId, studentId) = await SeedBaseDataAsync(context, testUser, testEmail);
                
                var homework = new Homework
                {
                    ClassId = classId,
                    Title = "Homework 2",
                    Status = 1,
                    DueDate = DateTime.UtcNow.AddDays(1)
                };
                context.Homeworks.Add(homework);
                
                var existingSubmission = new HomeworkSubmission
                {
                    HomeworkId = homework.Id,
                    StudentId = studentId,
                    Content = "Old answers",
                    Score = 8,
                    TeacherFeedback = "Good",
                    Status = 2
                };
                context.HomeworkSubmissions.Add(existingSubmission);
                
                await context.SaveChangesAsync();
                homeworkId = homework.Id;
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateHomeworkService(context);

                var dto = new HomeworkSubmissionSaveDto
                {
                    HomeworkId = homeworkId,
                    Content = "Updated answers"
                };

                var response = await service.SubmitHomeworkAsync(dto, testUser);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data!.Content.Should().Be("Updated answers");
                
                var submission = await context.HomeworkSubmissions.FirstOrDefaultAsync(s => s.HomeworkId == homeworkId);
                submission!.Score.Should().BeNull();
                submission.TeacherFeedback.Should().BeNull();
                submission.Status.Should().Be(1);
            }
        }

        [Fact]
        public async Task Abnormal_SubmitHomeworkAsync_StudentNotFound_ShouldReturnBadRequest()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateHomeworkService(context);
                var dto = new HomeworkSubmissionSaveDto { HomeworkId = 1, Content = "Test" };
                
                var response = await service.SubmitHomeworkAsync(dto, "unknown_user");

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.Message.Should().Be("Khong xac dinh duoc sinh vien");
            }
        }

        [Fact]
        public async Task Abnormal_SubmitHomeworkAsync_ZeroHomeworkId_ShouldReturnBadRequest()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            string testUser = "student_3";
            string testEmail = "student3@test.com";

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                await SeedBaseDataAsync(context, testUser, testEmail);
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateHomeworkService(context);
                var dto = new HomeworkSubmissionSaveDto { HomeworkId = 0, Content = "Test" };
                
                var response = await service.SubmitHomeworkAsync(dto, testUser);

                // Assert
                response.Success.Should().BeFalse();
                response.Message.Should().Be("ERR_HOMEWORK_ID_REQUIRED");
            }
        }

        [Fact]
        public async Task Abnormal_SubmitHomeworkAsync_HomeworkClosed_ShouldReturnBadRequest()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            string testUser = "student_4";
            string testEmail = "student4@test.com";
            int homeworkId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (classId, _) = await SeedBaseDataAsync(context, testUser, testEmail);
                
                var homework = new Homework
                {
                    ClassId = classId,
                    Title = "Homework closed",
                    Status = 0 // Closed
                };
                context.Homeworks.Add(homework);
                await context.SaveChangesAsync();
                homeworkId = homework.Id;
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateHomeworkService(context);
                var dto = new HomeworkSubmissionSaveDto { HomeworkId = homeworkId, Content = "Test" };
                
                var response = await service.SubmitHomeworkAsync(dto, testUser);

                // Assert
                response.Success.Should().BeFalse();
                response.Message.Should().Be("ERR_HOMEWORK_CLOSED");
            }
        }

        [Fact]
        public async Task Abnormal_SubmitHomeworkAsync_HomeworkPastDueDate_ShouldReturnBadRequest()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            string testUser = "student_5";
            string testEmail = "student5@test.com";
            int homeworkId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (classId, _) = await SeedBaseDataAsync(context, testUser, testEmail);
                
                var homework = new Homework
                {
                    ClassId = classId,
                    Title = "Homework past due",
                    Status = 1,
                    DueDate = DateTime.UtcNow.AddDays(-1) // Quá hạn
                };
                context.Homeworks.Add(homework);
                await context.SaveChangesAsync();
                homeworkId = homework.Id;
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateHomeworkService(context);
                var dto = new HomeworkSubmissionSaveDto { HomeworkId = homeworkId, Content = "Test" };
                
                var response = await service.SubmitHomeworkAsync(dto, testUser);

                // Assert
                response.Success.Should().BeFalse();
                response.Message.Should().Be("ERR_SUBMISSION_CLOSED");
            }
        }

        [Fact]
        public async Task Abnormal_SubmitHomeworkAsync_StudentNotEnrolled_ShouldReturnForbidden()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            string testUser = "student_6";
            string testEmail = "student6@test.com";
            int homeworkId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                // Student exists but not enrolled in the class of the homework
                var user = new IdentityUser { UserName = testUser, Email = testEmail };
                context.Users.Add(user);
                var student = new Student { Code = "ST006", Email = testEmail, Status = 1 };
                context.Students.Add(student);

                var homework = new Homework { ClassId = 999, Title = "Other Class HW", Status = 1, DueDate = DateTime.UtcNow.AddDays(1) };
                context.Homeworks.Add(homework);
                
                await context.SaveChangesAsync();
                homeworkId = homework.Id;
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateHomeworkService(context);
                var dto = new HomeworkSubmissionSaveDto { HomeworkId = homeworkId, Content = "Test" };
                
                var response = await service.SubmitHomeworkAsync(dto, testUser);

                // Assert
                response.Success.Should().BeFalse();
                response.Message.Should().Be("ERR_HOMEWORK_STUDENT_NOT_ENROLLED");
            }
        }
        
        [Fact]
        public async Task Abnormal_SubmitHomeworkAsync_NoContentOrAttachment_ShouldReturnBadRequest()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            string testUser = "student_7";
            string testEmail = "student7@test.com";
            int homeworkId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (classId, _) = await SeedBaseDataAsync(context, testUser, testEmail);
                
                var homework = new Homework
                {
                    ClassId = classId,
                    Title = "Homework",
                    Status = 1,
                    DueDate = DateTime.UtcNow.AddDays(1)
                };
                context.Homeworks.Add(homework);
                await context.SaveChangesAsync();
                homeworkId = homework.Id;
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateHomeworkService(context);
                var dto = new HomeworkSubmissionSaveDto { HomeworkId = homeworkId, Content = "", AttachmentUrls = null }; // Empty content
                
                var response = await service.SubmitHomeworkAsync(dto, testUser);

                // Assert
                response.Success.Should().BeFalse();
                response.Message.Should().Be("ERR_HOMEWORK_SUBMISSION_CONTENT_REQUIRED");
            }
        }
    }
}
