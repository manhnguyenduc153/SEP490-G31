using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using FluentAssertions;
using sep490_be.DTO.Course;
using sep490_be.Models;
using sep490_be.Repositories.Common;
using sep490_be.Repositories.Implementations;
using sep490_be.Services.Implementations;
using Xunit;

namespace sep490_be.Tests.Services
{
    public class CourseServiceTests
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

        private static CourseService CreateService(ApplicationDbContext context)
        {
            var uow = new UnitOfWork<ApplicationDbContext>(context);
            var repo = new CourseRepository(context, uow);
            return new CourseService(repo);
        }

        #region Normal Test Cases (Kiểm thử giá trị thông thường)

        [Fact]
        public async Task Normal_GetAllAsync_WithKeywordAndStatusFilters_ShouldReturnMatchingCourses()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                context.Courses.AddRange(
                    new Course { Code = "IELTS60", Name = "IELTS Intermediate 6.0", Status = 1, TextSearch = "IELTS60 IELTS Intermediate 6.0" },
                    new Course { Code = "IELTS75", Name = "IELTS Advanced 7.5", Status = 1, TextSearch = "IELTS75 IELTS Advanced 7.5" },
                    new Course { Code = "TOEIC800", Name = "TOEIC Target 800", Status = 0, TextSearch = "TOEIC800 TOEIC Target 800" }
                );
                await context.SaveChangesAsync();
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateService(context);
                
                // Test search by keyword
                var responseKeyword = await service.GetAllAsync(new CourseSearchDto
                {
                    Keyword = "IELTS",
                    PageIndex = 1,
                    PageSize = 10
                });

                // Test search by status
                var responseStatus = await service.GetAllAsync(new CourseSearchDto
                {
                    Status = false, // Status = 0
                    PageIndex = 1,
                    PageSize = 10
                });

                // Assert
                responseKeyword.Success.Should().BeTrue();
                responseKeyword.Data.Should().NotBeNull();
                responseKeyword.Data!.Items.Should().HaveCount(2);
                responseKeyword.Data.Items.Select(c => c.Code).Should().Contain(new[] { "IELTS60", "IELTS75" });

                responseStatus.Success.Should().BeTrue();
                responseStatus.Data!.Items.Should().ContainSingle();
                responseStatus.Data.Items.First().Code.Should().Be("TOEIC800");
            }
        }

        [Fact]
        public async Task Normal_GetByIdAsync_WhenCourseExists_ShouldReturnCourse()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int courseId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var course = new Course { Code = "IELTS60", Name = "IELTS Intermediate 6.0", Status = 1 };
                context.Courses.Add(course);
                await context.SaveChangesAsync();
                courseId = course.Id;
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateService(context);
                var response = await service.GetByIdAsync(courseId);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data.Should().NotBeNull();
                response.Data!.Code.Should().Be("IELTS60");
                response.Data.Name.Should().Be("IELTS Intermediate 6.0");
            }
        }

        [Fact]
        public async Task Normal_CreateAsync_WithValidInputs_ShouldCreateCourse()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            var dto = new CourseSaveDto
            {
                Code = "NEW_COURSE",
                Name = "New Course Name",
                Status = 1,
                Duration = 24,
                Price = 5000000,
                Description = "A valid description for new course"
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateService(context);
                var response = await service.CreateAsync(dto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.StatusCode.Should().Be(StatusCodes.Status201Created); // Based on ApiResponse.Created mapping
                response.Data.Should().NotBeNull();
                response.Data!.Code.Should().Be("NEW_COURSE");

                var exists = await context.Courses.AnyAsync(c => c.Code == "NEW_COURSE");
                exists.Should().BeTrue();
            }
        }

        [Fact]
        public async Task Normal_EditAsync_WithValidInputs_ShouldUpdateCourse()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int courseId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var course = new Course { Code = "IELTS60", Name = "IELTS Intermediate 6.0", Status = 1 };
                context.Courses.Add(course);
                await context.SaveChangesAsync();
                courseId = course.Id;
            }

            var dto = new CourseSaveDto
            {
                Id = courseId,
                Code = "IELTS60_UPD",
                Name = "IELTS 6.0 Updated",
                Status = 1,
                Duration = 30,
                Price = 6000000,
                Description = "Updated description"
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateService(context);
                var response = await service.EditAsync(dto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data.Should().NotBeNull();
                response.Data!.Code.Should().Be("IELTS60_UPD");
                response.Data.Duration.Should().Be(30);

                var updated = await context.Courses.FindAsync(courseId);
                updated.Should().NotBeNull();
                updated!.Code.Should().Be("IELTS60_UPD");
                updated.Duration.Should().Be(30);
            }
        }

        [Fact]
        public async Task Normal_DeleteAsync_WhenCourseExists_ShouldSoftDeleteCourse()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int courseId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var course = new Course { Code = "IELTS60", Name = "IELTS Intermediate 6.0", Status = 1 };
                context.Courses.Add(course);
                await context.SaveChangesAsync();
                courseId = course.Id;
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateService(context);
                var response = await service.DeleteAsync(courseId);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data.Should().BeTrue();

                var deletedCourse = await context.Courses.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == courseId);
                deletedCourse.Should().NotBeNull();
                deletedCourse!.IsDeleted.Should().BeTrue();
            }
        }

        [Fact]
        public async Task Normal_DeactiveAsync_WhenCourseExists_ShouldDeactivateCourse()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int courseId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var course = new Course { Code = "IELTS60", Name = "IELTS Intermediate 6.0", Status = 1 };
                context.Courses.Add(course);
                await context.SaveChangesAsync();
                courseId = course.Id;
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateService(context);
                var response = await service.DeactiveAsync(courseId);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data.Should().BeTrue();

                var deactivatedCourse = await context.Courses.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == courseId);
                deactivatedCourse.Should().NotBeNull();
                deactivatedCourse!.IsDeleted.Should().BeTrue();
            }
        }

        #endregion

        #region Boundary Test Cases (Kiểm thử giá trị biên)

        [Fact]
        public async Task Boundary_CreateAsync_WithCodeExactly50Characters_ShouldCreateCourse()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            var code50 = new string('C', 50);

            var dto = new CourseSaveDto
            {
                Code = code50,
                Name = "Boundary Code Course",
                Status = 1
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateService(context);
                var response = await service.CreateAsync(dto);

                // Assert
                response.Success.Should().BeTrue();
                response.Data!.Code.Should().HaveLength(50);
            }
        }

        [Fact]
        public async Task Boundary_CreateAsync_WithNameExactly200Characters_ShouldCreateCourse()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            var name200 = new string('N', 200);

            var dto = new CourseSaveDto
            {
                Code = "B_NAME",
                Name = name200,
                Status = 1
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateService(context);
                var response = await service.CreateAsync(dto);

                // Assert
                response.Success.Should().BeTrue();
                response.Data!.Name.Should().HaveLength(200);
            }
        }

        [Fact]
        public async Task Boundary_CreateAsync_WithDescriptionExactly1000Characters_ShouldCreateCourse()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            var desc1000 = new string('D', 1000);

            var dto = new CourseSaveDto
            {
                Code = "B_DESC",
                Name = "Boundary Desc Course",
                Status = 1,
                Description = desc1000
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateService(context);
                var response = await service.CreateAsync(dto);

                // Assert
                response.Success.Should().BeTrue();
                response.Data!.Description.Should().HaveLength(1000);
            }
        }

        #endregion

        #region Abnormal Test Cases (Kiểm thử giá trị bất thường / Lỗi)

        [Fact]
        public async Task Abnormal_GetByIdAsync_WhenCourseDoesNotExist_ShouldReturnNotFound()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateService(context);
                var response = await service.GetByIdAsync(9999);

                // Assert
                response.Success.Should().BeFalse();
                response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
                response.Message.Should().Be("ERR_COURSE_NOT_FOUND");
            }
        }

        [Fact]
        public async Task Abnormal_CreateAsync_WithNullOrEmptyCode_ShouldReturnBadRequest()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            var dto = new CourseSaveDto { Code = "", Name = "Course Without Code" };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateService(context);
                var response = await service.CreateAsync(dto);

                // Assert
                response.Success.Should().BeFalse();
                response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
                response.Message.Should().Be("ERR_CODE_EMPTY");
            }
        }

        [Fact]
        public async Task Abnormal_CreateAsync_WithCodeTooLong_ShouldReturnBadRequest()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            var code51 = new string('C', 51);

            var dto = new CourseSaveDto { Code = code51, Name = "Too Long Code" };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateService(context);
                var response = await service.CreateAsync(dto);

                // Assert
                response.Success.Should().BeFalse();
                response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
                response.Message.Should().Be("ERR_CODE_MAX_LENGTH");
            }
        }

        [Fact]
        public async Task Abnormal_CreateAsync_WithDuplicateCode_ShouldReturnBadRequest()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                context.Courses.Add(new Course { Code = "DUP_CODE", Name = "Existing Course", Status = 1 });
                await context.SaveChangesAsync();
            }

            var dto = new CourseSaveDto { Code = "DUP_CODE", Name = "New Duplicate Course" };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateService(context);
                var response = await service.CreateAsync(dto);

                // Assert
                response.Success.Should().BeFalse();
                response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
                response.Message.Should().Be("ERR_CODE_DUPLICATE");
            }
        }

        [Fact]
        public async Task Abnormal_CreateAsync_WithDuplicateName_ShouldReturnBadRequest()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                context.Courses.Add(new Course { Code = "CODE1", Name = "DUP_NAME", Status = 1 });
                await context.SaveChangesAsync();
            }

            var dto = new CourseSaveDto { Code = "CODE2", Name = "DUP_NAME" };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateService(context);
                var response = await service.CreateAsync(dto);

                // Assert
                response.Success.Should().BeFalse();
                response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
                response.Message.Should().Be("ERR_NAME_DUPLICATE");
            }
        }

        [Fact]
        public async Task Abnormal_CreateAsync_WithInvalidDuration_ShouldReturnBadRequest()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            var dto = new CourseSaveDto { Code = "VALID_C", Name = "Valid N", Duration = -5 };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateService(context);
                var response = await service.CreateAsync(dto);

                // Assert
                response.Success.Should().BeFalse();
                response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
                response.Message.Should().Be("ERR_DURATION_INVALID");
            }
        }

        [Fact]
        public async Task Abnormal_CreateAsync_WithInvalidPrice_ShouldReturnBadRequest()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            var dto = new CourseSaveDto { Code = "VALID_C", Name = "Valid N", Price = -100 };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateService(context);
                var response = await service.CreateAsync(dto);

                // Assert
                response.Success.Should().BeFalse();
                response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
                response.Message.Should().Be("ERR_PRICE_INVALID");
            }
        }

        [Fact]
        public async Task Abnormal_EditAsync_WhenCourseDoesNotExist_ShouldReturnNotFound()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            var dto = new CourseSaveDto { Id = 9999, Code = "EDIT_C", Name = "Edit N" };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateService(context);
                var response = await service.EditAsync(dto);

                // Assert
                response.Success.Should().BeFalse();
                response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
                response.Message.Should().Be("ERR_COURSE_NOT_FOUND");
            }
        }

        [Fact]
        public async Task Abnormal_DeleteAsync_WhenCourseDoesNotExist_ShouldReturnNotFound()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateService(context);
                var response = await service.DeleteAsync(9999);

                // Assert
                response.Success.Should().BeFalse();
                response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
                response.Message.Should().Be("ERR_COURSE_NOT_FOUND");
            }
        }

        [Fact]
        public async Task Abnormal_DeactiveAsync_WhenCourseDoesNotExist_ShouldReturnNotFound()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateService(context);
                var response = await service.DeactiveAsync(9999);

                // Assert
                response.Success.Should().BeFalse();
                response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
                response.Message.Should().Be("ERR_COURSE_NOT_FOUND");
            }
        }

        #endregion
    }
}
