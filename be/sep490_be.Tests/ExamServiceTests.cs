using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using FluentAssertions;
using Moq;
using Xunit;
using sep490_be.DTO;
using sep490_be.DTO.Exam;
using sep490_be.Models;
using sep490_be.Enums;
using sep490_be.Services.Implementations;

namespace sep490_be.Tests.Services
{
    /// <summary>
    /// Unit test suite for ExamService.
    /// Code Module: ExamService
    /// </summary>
    public class ExamServiceTests
    {
        private DbContextOptions<ApplicationDbContext> CreateNewContextOptions()
        {
            return new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }

        private Mock<IHttpContextAccessor> GetMockHttpContextAccessor()
        {
            return new Mock<IHttpContextAccessor>();
        }

        #region Normal Test Cases (Kiểm thử giá trị thông thường)

        [Fact]
        public async Task Normal_GetAllAsync_WithKeyword_ShouldReturnFilteredResults()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var exam1 = new Exam { Code = "EX01", Name = "Math Final", Title = "Math Final", Status = 1, Type = 1, IsDeleted = false };
                var exam2 = new Exam { Code = "EX02", Name = "Physics Mid", Title = "Physics Mid", Status = 1, Type = 1, IsDeleted = false };
                context.Exams.AddRange(exam1, exam2);
                await context.SaveChangesAsync();
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = new ExamService(context);
                var searchDto = new ExamSearchDto
                {
                    Keyword = "Math",
                    PageNumber = 1,
                    PageSize = 10
                };

                var response = await service.GetAllAsync(searchDto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Message.Should().Be("GET_EXAM_LIST_SUCCESS");
                response.Data.Should().NotBeNull();
                response.Data!.Items.Should().HaveCount(1);
                response.Data!.Items.First().Title.Should().Be("Math Final");
            }
        }

        [Fact]
        public async Task Normal_GetByIdAsync_ShouldReturnExamDetails()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int examId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var exam = new Exam { Code = "EX01", Name = "Math Final", Title = "Math Final", Status = 1, Type = 1, IsDeleted = false };
                context.Exams.Add(exam);
                await context.SaveChangesAsync();
                examId = exam.Id;
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = new ExamService(context);
                var response = await service.GetByIdAsync(examId);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Message.Should().Be("GET_EXAM_DETAIL_SUCCESS");
                response.Data.Should().NotBeNull();
                response.Data!.Id.Should().Be(examId);
            }
        }

        [Fact]
        public async Task Normal_CreateAsync_ShouldSaveExamAndQuestions()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var q1 = new Question { Code = "Q01", Name = "Q1", Content = "C1", QuestionType = 1, DifficultyLevel = 1, Status = 1 };
                var q2 = new Question { Code = "Q02", Name = "Q2", Content = "C2", QuestionType = 1, DifficultyLevel = 1, Status = 1 };
                context.Questions.AddRange(q1, q2);
                await context.SaveChangesAsync();
            }

            var saveDto = new ExamSaveDto
            {
                Title = "New Exam",
                Description = "New Description",
                Type = 1,
                TotalScore = 10,
                PassingScore = 5,
                MaxAttempts = 3,
                Status = 1,
                QuestionIds = new List<int> { 1, 2 }
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = new ExamService(context);
                var response = await service.CreateAsync(saveDto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Message.Should().Be("CREATE_EXAM_SUCCESS");
                response.Data.Should().NotBeNull();
                response.Data!.Title.Should().Be("New Exam");
                response.Data!.QuestionCount.Should().Be(2);
            }
        }

        [Fact]
        public async Task Normal_EditAsync_ShouldUpdateExamPropertiesAndQuestions()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int examId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var exam = new Exam { Code = "EX01", Name = "Old Title", Title = "Old Title", Status = 1, Type = 1, IsDeleted = false };
                var q1 = new Question { Code = "Q01", Name = "Q1", Content = "C1", QuestionType = 1, DifficultyLevel = 1, Status = 1 };
                context.Exams.Add(exam);
                context.Questions.Add(q1);
                await context.SaveChangesAsync();
                examId = exam.Id;

                var eq = new ExamQuestion { ExamId = examId, QuestionId = q1.Id, Point = 10.0m };
                context.ExamQuestions.Add(eq);
                await context.SaveChangesAsync();
            }

            var saveDto = new ExamSaveDto
            {
                Id = examId,
                Title = "Updated Title",
                Description = "Updated Desc",
                Type = 1,
                TotalScore = 100,
                PassingScore = 50,
                MaxAttempts = 2,
                Status = 1,
                QuestionIds = new List<int>() // Remove all questions
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = new ExamService(context);
                var response = await service.EditAsync(saveDto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Message.Should().Be("UPDATE_EXAM_SUCCESS");
                response.Data!.Title.Should().Be("Updated Title");
                response.Data!.TotalScore.Should().Be(100);
                response.Data!.QuestionCount.Should().Be(0);
            }
        }

        [Fact]
        public async Task Normal_DeleteAsync_WhenNoAttempts_ShouldHardDeleteExam()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int examId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var exam = new Exam { Code = "EX01", Name = "Final Exam", Title = "Final Exam", Status = 1, Type = 1, IsDeleted = false };
                context.Exams.Add(exam);
                await context.SaveChangesAsync();
                examId = exam.Id;
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = new ExamService(context);
                var response = await service.DeleteAsync(examId);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Message.Should().Be("DELETE_EXAM_SUCCESS");
                response.Data.Should().BeTrue();

                var exam = await context.Exams.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Id == examId);
                exam.Should().BeNull();
            }
        }

        [Fact]
        public async Task Normal_CopyAsync_ShouldDuplicateExamWithQuestions()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int examId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var exam = new Exam { Code = "EX01", Name = "Math Exam", Title = "Math Exam", Status = 1, Type = 1, IsDeleted = false, TotalScore = 10.0m };
                var q1 = new Question { Code = "Q01", Name = "Q1", Content = "C1", QuestionType = 1, DifficultyLevel = 1, Status = 1 };
                context.Exams.Add(exam);
                context.Questions.Add(q1);
                await context.SaveChangesAsync();
                examId = exam.Id;

                var eq = new ExamQuestion { ExamId = examId, QuestionId = q1.Id, Point = 10.0m };
                context.ExamQuestions.Add(eq);
                await context.SaveChangesAsync();
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = new ExamService(context);
                var response = await service.CopyAsync(examId);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Message.Should().Be("COPY_EXAM_SUCCESS");
                response.Data.Should().NotBeNull();
                response.Data!.Title.Should().Be("[Copy] Math Exam");
                response.Data!.Status.Should().Be(2); // Draft
                response.Data!.QuestionCount.Should().Be(1);
            }
        }

        [Fact]
        public async Task Normal_StartAttemptAsync_ShouldReturnInProgressAttempt()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int examId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var s = new Student { Code = "ST001", Name = "Nguyen Van A", Email = "a@test.com", Status = 1 };
                var e = new Exam { Code = "EX01", Name = "Quiz", Title = "Quiz", Status = 1, Type = 1, MaxAttempts = 3, IsDeleted = false };
                context.Students.Add(s);
                context.Exams.Add(e);
                await context.SaveChangesAsync();
                examId = e.Id;
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = new ExamService(context);
                var response = await service.StartAttemptAsync(examId, "ST001");

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Message.Should().Be("EXAM_ATTEMPT_STARTED");
                response.Data.Should().NotBeNull();
                response.Data!.Status.Should().Be(1); // In progress
            }
        }

        [Fact]
        public async Task Normal_StartAttemptAsync_WhenInProgressAttemptExists_ShouldReturnExistingAttempt()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int examId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var s = new Student { Code = "ST001", Name = "Nguyen Van A", Email = "a@test.com", Status = 1 };
                var e = new Exam { Code = "EX01", Name = "Quiz", Title = "Quiz", Status = 1, Type = 1, MaxAttempts = 3, IsDeleted = false };
                context.Students.Add(s);
                context.Exams.Add(e);
                await context.SaveChangesAsync();
                examId = e.Id;

                var att = new ExamAttempt { ExamId = examId, StudentId = s.Id, Status = 1, StartTime = DateTime.UtcNow };
                context.ExamAttempts.Add(att);
                await context.SaveChangesAsync();
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = new ExamService(context);
                var response = await service.StartAttemptAsync(examId, "ST001");

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Message.Should().Be("EXAM_ATTEMPT_CONTINUE");
                response.Data.Should().NotBeNull();
                response.Data!.Status.Should().Be(1);
            }
        }

        [Fact]
        public async Task Normal_SubmitAttemptAsync_ShouldGradeAndRecordAnswers()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int examId;
            int attemptId;
            int questionId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var s = new Student { Code = "ST001", Name = "Nguyen Van A", Email = "a@test.com", Status = 1 };
                var e = new Exam { Code = "EX01", Name = "Quiz", Title = "Quiz", Status = 1, Type = 1, IsDeleted = false, TotalScore = 10.0m };
                var q = new Question { Code = "Q01", Name = "Q1", Content = "C1", QuestionType = (int)QuestionType.SingleChoice, DifficultyLevel = 1, Status = 1 };
                q.QuestionAnswers.Add(new QuestionAnswer { Content = "Correct Choice", IsCorrect = true, Code = "QA01", Name = "Correct" });
                
                context.Students.Add(s);
                context.Exams.Add(e);
                context.Questions.Add(q);
                await context.SaveChangesAsync();
                
                examId = e.Id;
                questionId = q.Id;

                var eq = new ExamQuestion { ExamId = examId, QuestionId = q.Id, Point = 10.0m };
                context.ExamQuestions.Add(eq);

                var att = new ExamAttempt { ExamId = examId, StudentId = s.Id, Status = 1, StartTime = DateTime.UtcNow };
                context.ExamAttempts.Add(att);

                await context.SaveChangesAsync();
                attemptId = att.Id;
            }

            var submitDto = new ExamSubmitDto
            {
                AttemptId = attemptId,
                Answers = new List<ExamSubmitAnswerDto>
                {
                    new ExamSubmitAnswerDto
                    {
                        QuestionId = questionId,
                        AnswerContent = "Correct Choice"
                    }
                }
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = new ExamService(context);
                var response = await service.SubmitAttemptAsync(examId, submitDto, "a@test.com");

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Message.Should().Be("SUBMIT_EXAM_SUCCESS");
                response.Data.Should().NotBeNull();
                response.Data!.Score.Should().Be(10.0m);
                response.Data!.Status.Should().Be(2); // Submitted
            }
        }

        [Fact]
        public async Task Normal_GradeAttemptAsync_ShouldUpdateScoreAndTeacherComment()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int attemptId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var s = new Student { Code = "ST001", Name = "Nguyen Van A", Email = "a@test.com", Status = 1 };
                var e = new Exam { Code = "EX01", Name = "Writing Test", Title = "Writing Test", Status = 1, Type = 3, IsDeleted = false };
                context.Students.Add(s);
                context.Exams.Add(e);
                await context.SaveChangesAsync();

                var att = new ExamAttempt { ExamId = e.Id, StudentId = s.Id, Status = 1, StartTime = DateTime.UtcNow };
                context.ExamAttempts.Add(att);
                await context.SaveChangesAsync();

                var ans = new ExamAnswer { ExamAttemptId = att.Id, QuestionId = 1, AnswerContent = "My Essay", Code = "ANS01", Name = "Ans" };
                context.ExamAnswers.Add(ans);
                await context.SaveChangesAsync();
                attemptId = att.Id;
            }

            var gradeDto = new GradeAttemptDto
            {
                Score = 8.5m,
                TeacherComment = "Good work"
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = new ExamService(context);
                var response = await service.GradeAttemptAsync(attemptId, gradeDto, "teacher@test.com");

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Message.Should().Be("GRADE_ATTEMPT_SUCCESS");
                response.Data!.Score.Should().Be(8.5m);
                response.Data!.TeacherComment.Should().Be("Good work");
            }
        }

        [Fact]
        public async Task Normal_GetStudentExamsAsync_ShouldReturnExamsForEnrolledClasses()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var cls = new Class { Name = "Class 10A", Status = 1, Code = "C10A" };
                var s = new Student { Code = "ST001", Name = "Nguyen Van A", Email = "student@test.com", Status = 1 };
                var sc = new StudentClass { Student = s, Class = cls, Status = (int)StudentClassStatus.Studying };
                var e = new Exam { Code = "EX01", Name = "Final Test", Title = "Final Test", Class = cls, Status = 1, Type = 1, IsDeleted = false };

                context.Classes.Add(cls);
                context.Students.Add(s);
                context.StudentClasses.Add(sc);
                context.Exams.Add(e);
                await context.SaveChangesAsync();
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = new ExamService(context);
                var response = await service.GetStudentExamsAsync("student@test.com");

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Message.Should().Be("GET_STUDENT_EXAMS_SUCCESS");
                response.Data.Should().NotBeEmpty();
                response.Data!.First().Title.Should().Be("Final Test");
            }
        }

        #endregion

        #region Boundary Test Cases (Kiểm thử giá trị biên)

        [Fact]
        public async Task Boundary_CreateAsync_WithHighPassingScore_ShouldSaveSuccessfully()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            var saveDto = new ExamSaveDto
            {
                Title = "Boundary Exam",
                Type = 1,
                TotalScore = 100,
                PassingScore = 100, // Passing score equals total score
                Status = 1
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = new ExamService(context);
                var response = await service.CreateAsync(saveDto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data!.PassingScore.Should().Be(100);
            }
        }

        [Fact]
        public async Task Boundary_CreateAsync_InvalidDuration_ShouldReturnFail()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            var saveDto = new ExamSaveDto
            {
                Title = "Boundary Exam Invalid Duration",
                Duration = 0 // Invalid duration <= 0
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = new ExamService(context);
                var response = await service.CreateAsync(saveDto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.Message.Should().Be("ERR_DURATION_INVALID");
            }
        }

        [Fact]
        public async Task Boundary_CreateAsync_InvalidScore_ShouldReturnFail()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            var saveDto = new ExamSaveDto
            {
                Title = "Boundary Exam Invalid Score",
                TotalScore = 10,
                PassingScore = 20 // Passing score > total score
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = new ExamService(context);
                var response = await service.CreateAsync(saveDto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.Message.Should().Be("ERR_SCORE_INVALID");
            }
        }

        #endregion

        #region Abnormal Test Cases (Kiểm thử giá trị bất thường)

        [Fact]
        public async Task Abnormal_GetByIdAsync_NotFound_ShouldReturnFail()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = new ExamService(context);
                var response = await service.GetByIdAsync(9999);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.Message.Should().Be("ERR_EXAM_NOT_FOUND");
            }
        }

        [Fact]
        public async Task Abnormal_DeleteAsync_WhenExamInUse_ShouldReturnFail()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int examId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var s = new Student { Code = "ST01", Name = "Student A", Email = "st@test.com", Status = 1 };
                var e = new Exam { Code = "EX01", Name = "Exam in use", Title = "Exam in use", Status = 1, Type = 1, IsDeleted = false };
                context.Students.Add(s);
                context.Exams.Add(e);
                await context.SaveChangesAsync();
                examId = e.Id;

                var att = new ExamAttempt { ExamId = examId, StudentId = s.Id, Status = 2, StartTime = DateTime.UtcNow };
                context.ExamAttempts.Add(att);
                await context.SaveChangesAsync();
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = new ExamService(context);
                var response = await service.DeleteAsync(examId);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.Message.Should().Be("ERR_EXAM_IN_USE");
            }
        }

        [Fact]
        public async Task Abnormal_StartAttemptAsync_StudentNotFound_ShouldFail()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = new ExamService(context);
                var response = await service.StartAttemptAsync(1, "NON_EXISTENT_STUDENT");

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.Message.Should().Be("ERR_STUDENT_NOT_FOUND");
            }
        }

        [Fact]
        public async Task Abnormal_StartAttemptAsync_ExamNotFound_ShouldFail()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var s = new Student { Code = "ST01", Name = "A", Email = "a@test.com", Status = 1 };
                context.Students.Add(s);
                await context.SaveChangesAsync();
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = new ExamService(context);
                var response = await service.StartAttemptAsync(9999, "ST01");

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.Message.Should().Be("ERR_EXAM_NOT_FOUND");
            }
        }

        [Fact]
        public async Task Abnormal_StartAttemptAsync_ExamNotPublished_ShouldFail()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int examId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var s = new Student { Code = "ST01", Name = "A", Email = "a@test.com", Status = 1 };
                var e = new Exam { Code = "EX01", Name = "Quiz", Title = "Quiz", Status = 2, Type = 1, IsDeleted = false }; // Status = 2 (Draft / Not published)
                context.Students.Add(s);
                context.Exams.Add(e);
                await context.SaveChangesAsync();
                examId = e.Id;
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = new ExamService(context);
                var response = await service.StartAttemptAsync(examId, "ST01");

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.Message.Should().Be("ERR_EXAM_NOT_PUBLISHED");
            }
        }

        [Fact]
        public async Task Abnormal_StartAttemptAsync_ExamClosed_ShouldFail()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int examId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var s = new Student { Code = "ST01", Name = "A", Email = "a@test.com", Status = 1 };
                var e = new Exam 
                { 
                    Code = "EX01", 
                    Name = "Quiz", 
                    Title = "Quiz", 
                    Status = 1, 
                    Type = 1, 
                    IsDeleted = false,
                    EndTime = DateTime.UtcNow.AddHours(-1), // Closed 1 hour ago
                    AllowLateSubmit = false
                };
                context.Students.Add(s);
                context.Exams.Add(e);
                await context.SaveChangesAsync();
                examId = e.Id;
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = new ExamService(context);
                var response = await service.StartAttemptAsync(examId, "ST01");

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.Message.Should().Be("ERR_EXAM_CLOSED");
            }
        }

        [Fact]
        public async Task Abnormal_StartAttemptAsync_MaxAttemptsExceeded_ShouldFail()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int examId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var s = new Student { Code = "ST01", Name = "A", Email = "a@test.com", Status = 1 };
                var e = new Exam { Code = "EX01", Name = "Quiz", Title = "Quiz", Status = 1, Type = 1, MaxAttempts = 1, IsDeleted = false };
                context.Students.Add(s);
                context.Exams.Add(e);
                await context.SaveChangesAsync();
                examId = e.Id;

                var att = new ExamAttempt { ExamId = examId, StudentId = s.Id, Status = 2, StartTime = DateTime.UtcNow };
                context.ExamAttempts.Add(att);
                await context.SaveChangesAsync();
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = new ExamService(context);
                var response = await service.StartAttemptAsync(examId, "ST01");

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.Message.Should().Be("ERR_MAX_ATTEMPTS_EXCEEDED");
            }
        }

        [Fact]
        public async Task Abnormal_SubmitAttemptAsync_AttemptNotFound_ShouldFail()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int examId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var s = new Student { Code = "ST01", Name = "A", Email = "a@test.com", Status = 1 };
                var e = new Exam { Code = "EX01", Name = "Quiz", Title = "Quiz", Status = 1, Type = 1, IsDeleted = false };
                context.Students.Add(s);
                context.Exams.Add(e);
                await context.SaveChangesAsync();
                examId = e.Id;
            }

            var submitDto = new ExamSubmitDto
            {
                AttemptId = 9999, // Non-existent attempt
                Answers = new List<ExamSubmitAnswerDto>()
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = new ExamService(context);
                var response = await service.SubmitAttemptAsync(examId, submitDto, "a@test.com");

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.Message.Should().Be("ERR_ATTEMPT_NOT_FOUND");
            }
        }

        [Fact]
        public async Task Abnormal_SubmitAttemptAsync_AlreadySubmitted_ShouldFail()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int examId;
            int attemptId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var s = new Student { Code = "ST01", Name = "A", Email = "a@test.com", Status = 1 };
                var e = new Exam { Code = "EX01", Name = "Quiz", Title = "Quiz", Status = 1, Type = 1, IsDeleted = false };
                context.Students.Add(s);
                context.Exams.Add(e);
                await context.SaveChangesAsync();
                examId = e.Id;

                var att = new ExamAttempt { ExamId = examId, StudentId = s.Id, Status = 2, StartTime = DateTime.UtcNow }; // Status 2 = Submitted
                context.ExamAttempts.Add(att);
                await context.SaveChangesAsync();
                attemptId = att.Id;
            }

            var submitDto = new ExamSubmitDto
            {
                AttemptId = attemptId,
                Answers = new List<ExamSubmitAnswerDto>()
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = new ExamService(context);
                var response = await service.SubmitAttemptAsync(examId, submitDto, "a@test.com");

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.Message.Should().Be("ERR_ATTEMPT_ALREADY_SUBMITTED");
            }
        }

        #endregion
    }
}
