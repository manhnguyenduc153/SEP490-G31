using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using Moq;
using Xunit;
using sep490_be.DTO;
using sep490_be.DTO.Question;
using sep490_be.Models;
using sep490_be.Enums;
using sep490_be.Repositories.Implementations;
using sep490_be.Repositories.Common;
using sep490_be.Services.Implementations;

namespace sep490_be.Tests.Services
{
    /// <summary>
    /// Unit test suite for QuestionService.
    /// Code Module: QuestionService
    /// </summary>
    public class QuestionServiceTests
    {
        private DbContextOptions<ApplicationDbContext> CreateNewContextOptions()
        {
            return new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
        }

        private Mock<IHttpContextAccessor> GetMockHttpContextAccessor()
        {
            return new Mock<IHttpContextAccessor>();
        }

        #region Normal Test Cases (Kiểm thử giá trị thông thường)

        [Fact]
        public async Task Normal_GetAllAsync_ShouldReturnFilteredResults()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var cat = new QuestionCategory { Name = "Math" };
                context.QuestionCategories.Add(cat);
                await context.SaveChangesAsync();

                var q1 = new Question { Code = "Q001", Name = "Addition", Content = "1+1=?", QuestionType = (int)QuestionType.SingleChoice, DifficultyLevel = (int)DifficultyLevel.Easy, CategoryId = cat.Id, Status = 1 };
                var q2 = new Question { Code = "Q002", Name = "History", Content = "Who was the first president?", QuestionType = (int)QuestionType.Essay, DifficultyLevel = (int)DifficultyLevel.Hard, Status = 1 };
                context.Questions.AddRange(q1, q2);
                await context.SaveChangesAsync();
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var repo = new QuestionRepository(context, uow);
                var catRepo = new QuestionCategoryRepository(context, uow);
                var service = new QuestionService(repo, catRepo);

                var searchDto = new QuestionSearchDto
                {
                    Keyword = "president",
                    PageIndex = 1,
                    PageSize = 10
                };

                var response = await service.GetAllAsync(searchDto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data.Should().NotBeNull();
                response.Data!.Items.Should().HaveCount(1);
                response.Data!.Items.First().Name.Should().Be("History");
            }
        }

        [Fact]
        public async Task Normal_GetByIdAsync_ShouldReturnQuestion()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int questionId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var q = new Question { Code = "Q001", Name = "Addition", Content = "1+1=?", QuestionType = (int)QuestionType.SingleChoice, DifficultyLevel = (int)DifficultyLevel.Easy, Status = 1 };
                context.Questions.Add(q);
                await context.SaveChangesAsync();
                questionId = q.Id;
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var repo = new QuestionRepository(context, uow);
                var catRepo = new QuestionCategoryRepository(context, uow);
                var service = new QuestionService(repo, catRepo);

                var response = await service.GetByIdAsync(questionId);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data.Should().NotBeNull();
                response.Data!.Id.Should().Be(questionId);
            }
        }

        [Fact]
        public async Task Normal_CreateAsync_ShouldSaveQuestion()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            var saveDto = new QuestionSaveDto
            {
                Code = "QTEST",
                Name = "A valid question",
                Content = "What is 2+2?",
                QuestionType = (int)QuestionType.SingleChoice,
                DifficultyLevel = (int)DifficultyLevel.Medium,
                Point = 1.5m,
                QuestionAnswers = new List<QuestionAnswerDto>
                {
                    new QuestionAnswerDto { Content = "4", IsCorrect = true },
                    new QuestionAnswerDto { Content = "5", IsCorrect = false }
                }
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var repo = new QuestionRepository(context, uow);
                var catRepo = new QuestionCategoryRepository(context, uow);
                var service = new QuestionService(repo, catRepo);

                var response = await service.CreateAsync(saveDto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Message.Should().Be("CREATE_QUESTION_SUCCESS");
                response.Data.Should().NotBeNull();
                response.Data!.Code.Should().Be("QTEST");
                response.Data!.Point.Should().Be(1.5m);
                response.Data!.QuestionAnswers.Should().HaveCount(2);
            }
        }

        [Fact]
        public async Task Normal_EditAsync_ShouldUpdateQuestion()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int questionId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var q = new Question
                {
                    Code = "Q001",
                    Name = "Old Title",
                    Content = "Old Content",
                    QuestionType = (int)QuestionType.SingleChoice,
                    DifficultyLevel = (int)DifficultyLevel.Easy,
                    Status = 1
                };
                q.QuestionAnswers.Add(new QuestionAnswer { Content = "Ans 1", IsCorrect = true, Code = "A1", Name = "Ans 1" });
                q.QuestionAnswers.Add(new QuestionAnswer { Content = "Ans 2", IsCorrect = false, Code = "A2", Name = "Ans 2" });
                context.Questions.Add(q);
                await context.SaveChangesAsync();
                questionId = q.Id;
            }

            var editDto = new QuestionSaveDto
            {
                Id = questionId,
                Code = "Q001",
                Name = "Updated Title",
                Content = "Updated Content",
                QuestionType = (int)QuestionType.SingleChoice,
                DifficultyLevel = (int)DifficultyLevel.Medium,
                Point = 2.0m,
                QuestionAnswers = new List<QuestionAnswerDto>
                {
                    new QuestionAnswerDto { Content = "New Correct Ans", IsCorrect = true },
                    new QuestionAnswerDto { Content = "New Incorrect Ans", IsCorrect = false }
                }
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var repo = new QuestionRepository(context, uow);
                var catRepo = new QuestionCategoryRepository(context, uow);
                var service = new QuestionService(repo, catRepo);

                var response = await service.EditAsync(editDto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data!.Name.Should().Be("Updated Title");
                response.Data!.Content.Should().Be("Updated Content");
                response.Data!.DifficultyLevel.Should().Be((int)DifficultyLevel.Medium);
                response.Data!.QuestionAnswers.Should().HaveCount(2);
                response.Data!.QuestionAnswers.First().Content.Should().Be("New Correct Ans");
            }
        }

        [Fact]
        public async Task Normal_DeleteAsync_ShouldSoftDeleteQuestion()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int questionId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var q = new Question { Code = "Q001", Name = "Addition", Content = "1+1=?", QuestionType = (int)QuestionType.SingleChoice, DifficultyLevel = (int)DifficultyLevel.Easy, Status = 1 };
                context.Questions.Add(q);
                await context.SaveChangesAsync();
                questionId = q.Id;
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var repo = new QuestionRepository(context, uow);
                var catRepo = new QuestionCategoryRepository(context, uow);
                var service = new QuestionService(repo, catRepo);

                var response = await service.DeleteAsync(questionId);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data.Should().BeTrue();

                // Assert soft delete
                var deletedItem = await context.Questions.IgnoreQueryFilters().FirstOrDefaultAsync(q => q.Id == questionId);
                deletedItem.Should().NotBeNull();
                deletedItem!.IsDeleted.Should().BeTrue();
            }
        }

        [Fact]
        public async Task Normal_DeactiveAsync_ShouldUpdateStatusToZero()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int questionId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var q = new Question { Code = "Q001", Name = "Addition", Content = "1+1=?", QuestionType = (int)QuestionType.SingleChoice, DifficultyLevel = (int)DifficultyLevel.Easy, Status = 1 };
                context.Questions.Add(q);
                await context.SaveChangesAsync();
                questionId = q.Id;
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var repo = new QuestionRepository(context, uow);
                var catRepo = new QuestionCategoryRepository(context, uow);
                var service = new QuestionService(repo, catRepo);

                var response = await service.DeactiveAsync(questionId);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data.Should().BeTrue();

                var question = await context.Questions.IgnoreQueryFilters().FirstOrDefaultAsync(q => q.Id == questionId);
                question!.IsDeleted.Should().BeTrue();
            }
        }

        [Fact]
        public async Task Normal_ImportAsync_ShouldInsertValidList()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var cat = new QuestionCategory { Name = "Science" };
                context.QuestionCategories.Add(cat);
                await context.SaveChangesAsync();
            }

            var importList = new List<QuestionSaveDto>
            {
                new QuestionSaveDto
                {
                    Code = "QIMP001",
                    Name = "Science Question",
                    Content = "What is H2O?",
                    QuestionType = (int)QuestionType.SingleChoice,
                    DifficultyLevel = (int)DifficultyLevel.Easy,
                    QuestionAnswers = new List<QuestionAnswerDto>
                    {
                        new QuestionAnswerDto { Content = "Water", IsCorrect = true },
                        new QuestionAnswerDto { Content = "Oxygen", IsCorrect = false }
                    }
                }
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var repo = new QuestionRepository(context, uow);
                var catRepo = new QuestionCategoryRepository(context, uow);
                var service = new QuestionService(repo, catRepo);

                var response = await service.ImportAsync(importList);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data.Should().NotBeNull();
                response.Data!.Should().HaveCount(1);
                response.Data!.First().Code.Should().Be("QIMP001");
            }
        }

        #endregion

        #region Boundary Test Cases (Kiểm thử giá trị biên)

        [Fact]
        public async Task Boundary_CreateAsync_WithTitleMaxLength_ShouldCreateSuccessfully()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            // Maximum name/title length is 200 characters
            var longTitle = new string('A', 200);

            var saveDto = new QuestionSaveDto
            {
                Code = "QMAX",
                Name = longTitle,
                Content = "Boundary test content",
                QuestionType = (int)QuestionType.Essay,
                DifficultyLevel = (int)DifficultyLevel.Easy
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var repo = new QuestionRepository(context, uow);
                var catRepo = new QuestionCategoryRepository(context, uow);
                var service = new QuestionService(repo, catRepo);

                var response = await service.CreateAsync(saveDto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data!.Name.Length.Should().Be(200);
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
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var repo = new QuestionRepository(context, uow);
                var catRepo = new QuestionCategoryRepository(context, uow);
                var service = new QuestionService(repo, catRepo);

                var response = await service.GetByIdAsync(9999);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.Message.Should().Be("ERR_QUESTION_NOT_FOUND");
            }
        }

        [Fact]
        public async Task Abnormal_CreateAsync_EmptyTitle_ShouldFailWithValidationError()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            var saveDto = new QuestionSaveDto
            {
                Code = "QERR",
                Name = "", // Empty Title
                Content = "Content",
                QuestionType = (int)QuestionType.Essay,
                DifficultyLevel = (int)DifficultyLevel.Easy
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var repo = new QuestionRepository(context, uow);
                var catRepo = new QuestionCategoryRepository(context, uow);
                var service = new QuestionService(repo, catRepo);

                var response = await service.CreateAsync(saveDto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.Message.Should().Be("ERR_TITLE_EMPTY");
            }
        }

        [Fact]
        public async Task Abnormal_CreateAsync_TitleTooLong_ShouldFailWithValidationError()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            var longTitle = new string('A', 201); // Too long

            var saveDto = new QuestionSaveDto
            {
                Code = "QERR",
                Name = longTitle,
                Content = "Content",
                QuestionType = (int)QuestionType.Essay,
                DifficultyLevel = (int)DifficultyLevel.Easy
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var repo = new QuestionRepository(context, uow);
                var catRepo = new QuestionCategoryRepository(context, uow);
                var service = new QuestionService(repo, catRepo);

                var response = await service.CreateAsync(saveDto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.Message.Should().Be("ERR_TITLE_MAX_LENGTH");
            }
        }

        [Fact]
        public async Task Abnormal_CreateAsync_DuplicateCode_ShouldFail()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var q = new Question { Code = "DUP01", Name = "First", Content = "Content 1", QuestionType = (int)QuestionType.Essay, DifficultyLevel = (int)DifficultyLevel.Easy, Status = 1 };
                context.Questions.Add(q);
                await context.SaveChangesAsync();
            }

            var saveDto = new QuestionSaveDto
            {
                Code = "DUP01", // Duplicate code
                Name = "Second",
                Content = "Content 2",
                QuestionType = (int)QuestionType.Essay,
                DifficultyLevel = (int)DifficultyLevel.Easy
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var repo = new QuestionRepository(context, uow);
                var catRepo = new QuestionCategoryRepository(context, uow);
                var service = new QuestionService(repo, catRepo);

                var response = await service.CreateAsync(saveDto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.Message.Should().Be("ERR_CODE_DUPLICATE");
            }
        }

        [Fact]
        public async Task Abnormal_CreateAsync_CategoryNotFound_ShouldFail()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            var saveDto = new QuestionSaveDto
            {
                Code = "QCAT",
                Name = "Math Question",
                Content = "What is 1+1?",
                QuestionType = (int)QuestionType.Essay,
                DifficultyLevel = (int)DifficultyLevel.Easy,
                CategoryId = 9999 // Non-existent category ID
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var repo = new QuestionRepository(context, uow);
                var catRepo = new QuestionCategoryRepository(context, uow);
                var service = new QuestionService(repo, catRepo);

                var response = await service.CreateAsync(saveDto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.Message.Should().Be("ERR_CATEGORY_NOT_FOUND");
            }
        }

        [Fact]
        public async Task Abnormal_CreateAsync_SingleChoice_WithoutExactlyOneCorrectAnswer_ShouldFail()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            var saveDto = new QuestionSaveDto
            {
                Code = "QANS",
                Name = "Single choice question",
                Content = "Question?",
                QuestionType = (int)QuestionType.SingleChoice,
                DifficultyLevel = (int)DifficultyLevel.Easy,
                QuestionAnswers = new List<QuestionAnswerDto>
                {
                    new QuestionAnswerDto { Content = "A", IsCorrect = true },
                    new QuestionAnswerDto { Content = "B", IsCorrect = true } // Two correct answers!
                }
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var repo = new QuestionRepository(context, uow);
                var catRepo = new QuestionCategoryRepository(context, uow);
                var service = new QuestionService(repo, catRepo);

                var response = await service.CreateAsync(saveDto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.Message.Should().Be("ERR_MUST_HAVE_EXACTLY_ONE_CORRECT_ANSWER");
            }
        }

        [Fact]
        public async Task Abnormal_CreateAsync_MultipleChoice_WithoutCorrectAnswer_ShouldFail()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            var saveDto = new QuestionSaveDto
            {
                Code = "QANS",
                Name = "Multiple choice question",
                Content = "Question?",
                QuestionType = (int)QuestionType.MultipleChoice,
                DifficultyLevel = (int)DifficultyLevel.Easy,
                QuestionAnswers = new List<QuestionAnswerDto>
                {
                    new QuestionAnswerDto { Content = "A", IsCorrect = false },
                    new QuestionAnswerDto { Content = "B", IsCorrect = false } // No correct answers!
                }
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var repo = new QuestionRepository(context, uow);
                var catRepo = new QuestionCategoryRepository(context, uow);
                var service = new QuestionService(repo, catRepo);

                var response = await service.CreateAsync(saveDto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.Message.Should().Be("ERR_MUST_HAVE_AT_LEAST_ONE_CORRECT_ANSWER");
            }
        }

        [Fact]
        public async Task Abnormal_DeleteAsync_NotFound_ShouldFail()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var repo = new QuestionRepository(context, uow);
                var catRepo = new QuestionCategoryRepository(context, uow);
                var service = new QuestionService(repo, catRepo);

                var response = await service.DeleteAsync(9999);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.Message.Should().Be("ERR_QUESTION_NOT_FOUND");
            }
        }

        [Fact]
        public async Task Normal_ImportAsync_EmptyList_ShouldReturnEmptyList()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var repo = new QuestionRepository(context, uow);
                var catRepo = new QuestionCategoryRepository(context, uow);
                var service = new QuestionService(repo, catRepo);

                var response = await service.ImportAsync(new List<QuestionSaveDto>());

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data.Should().BeEmpty();
            }
        }

        #endregion
    }
}
