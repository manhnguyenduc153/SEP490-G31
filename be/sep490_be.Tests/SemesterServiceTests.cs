using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using FluentAssertions;
using sep490_be.DTO.Semester;
using sep490_be.DTO.Student;
using sep490_be.Models;
using sep490_be.Services.Implementations;
using Xunit;

namespace sep490_be.Tests.Services
{
    /// <summary>
    /// Unit test suite for SemesterService mapping to Excel Guideline.
    /// Code Module: SemesterService
    /// </summary>
    public class SemesterServiceTests
    {
        private DbContextOptions<ApplicationDbContext> CreateNewContextOptions()
        {
            // Tạo cấu hình Database In-Memory độc lập cho mỗi test case
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
        public async Task Normal_GetAllAsync_WhenActiveSemestersExist_ShouldReturnList()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttpAccessor = GetMockHttpContextAccessor();

            using (var context = new ApplicationDbContext(options, mockHttpAccessor.Object))
            {
                var s1 = new Semester { Code = "SUMMER2026", Name = "Học kỳ Hè 2026", StartDate = DateTime.Now, EndDate = DateTime.Now.AddMonths(3) };
                var s2 = new Semester { Code = "SPRING2026", Name = "Học kỳ Xuân 2026", StartDate = DateTime.Now.AddMonths(-4), EndDate = DateTime.Now.AddMonths(-1) };
                context.Semesters.AddRange(s1, s2);
                await context.SaveChangesAsync();

                s2.IsDeleted = true;
                context.Entry(s2).State = EntityState.Modified;
                await context.SaveChangesAsync();
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttpAccessor.Object))
            {
                var service = new SemesterService(context);
                var response = await service.GetAllAsync();

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data.Should().HaveCount(1);
                response.Data[0].Code.Should().Be("SUMMER2026");
            }
        }

        [Fact]
        public async Task Normal_GetByIdAsync_WhenSemesterExists_ShouldReturnOk()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttpAccessor = GetMockHttpContextAccessor();
            int createdId;

            using (var context = new ApplicationDbContext(options, mockHttpAccessor.Object))
            {
                var s1 = new Semester { Code = "SUMMER2026", Name = "Học kỳ Hè 2026", StartDate = DateTime.Now, EndDate = DateTime.Now.AddMonths(3) };
                context.Semesters.Add(s1);
                await context.SaveChangesAsync();
                createdId = s1.Id;
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttpAccessor.Object))
            {
                var service = new SemesterService(context);
                var response = await service.GetByIdAsync(createdId);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data.Should().NotBeNull();
                response.Data.Code.Should().Be("SUMMER2026");
            }
        }

        [Fact]
        public async Task Normal_CreateAsync_WithValidInputs_ShouldReturnCreated()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttpAccessor = GetMockHttpContextAccessor();

            var newSemesterDto = new SemesterSaveDto
            {
                Code = "AUTUMN2026",
                Name = "Học kỳ Thu 2026",
                StartDate = DateTime.Now,
                EndDate = DateTime.Now.AddMonths(3)
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttpAccessor.Object))
            {
                var service = new SemesterService(context);
                var response = await service.CreateAsync(newSemesterDto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.StatusCode.Should().Be(StatusCodes.Status201Created); // 201 Created
                response.Data.Should().NotBeNull();
                response.Data!.Code.Should().Be("AUTUMN2026");
            }
        }

        [Fact]
        public async Task Normal_CreateStudentRegistrationAsync_WithValidDto_ShouldCreateRegistration()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttpAccessor = GetMockHttpContextAccessor();
            int semId;
            int courseId;

            using (var context = new ApplicationDbContext(options, mockHttpAccessor.Object))
            {
                var semester = new Semester { Code = "FALL2026", Name = "Kỳ học Thu 2026", StartDate = DateTime.Now, EndDate = DateTime.Now.AddMonths(3) };
                var course = new Course { Code = "IELTS75", Name = "IELTS Advanced 7.5", Status = 1 };
                context.Semesters.Add(semester);
                context.Courses.Add(course);
                await context.SaveChangesAsync();

                semId = semester.Id;
                courseId = course.Id;
            }

            var dto = new StudentRegistrationSaveDto
            {
                SemesterId = semId,
                CourseId = courseId,
                StudentEmail = "student@test.com",
                StudentName = "Nguyen Van Student",
                StudentPhone = "0900000000",
                PreferredSlots = new List<string> { "Morning", "Evening" },
                Status = 0
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttpAccessor.Object))
            {
                var service = new SemesterService(context);
                var response = await service.CreateStudentRegistrationAsync(dto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data.Should().NotBeNull();
                response.Data!.StudentEmail.Should().Be("student@test.com");
                response.Data.PreferredSlots.Should().Contain("Morning");
            }
        }

        [Fact]
        public async Task Normal_EditStudentRegistrationAsync_WithValidDto_ShouldUpdateRegistration()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttpAccessor = GetMockHttpContextAccessor();
            int semId;
            int courseId;
            int studentId;
            int regId;

            using (var context = new ApplicationDbContext(options, mockHttpAccessor.Object))
            {
                var semester = new Semester { Code = "FALL2026", Name = "Kỳ học Thu 2026", StartDate = DateTime.Now, EndDate = DateTime.Now.AddMonths(3) };
                var course = new Course { Code = "IELTS75", Name = "IELTS Advanced 7.5", Status = 1 };
                var student = new Student { Code = "ST001", Name = "Nguyen Student", Email = "student@test.com", Status = 1 };
                context.Semesters.Add(semester);
                context.Courses.Add(course);
                context.Students.Add(student);
                await context.SaveChangesAsync();

                semId = semester.Id;
                courseId = course.Id;
                studentId = student.Id;

                var reg = new StudentRegistration
                {
                    SemesterId = semId,
                    CourseId = courseId,
                    StudentId = studentId,
                    PreferredSlotsJson = "[\"Morning\"]",
                    Status = 0
                };
                context.StudentRegistrations.Add(reg);
                await context.SaveChangesAsync();
                regId = reg.Id;
            }

            var updateDto = new StudentRegistrationSaveDto
            {
                SemesterId = semId,
                CourseId = courseId,
                StudentEmail = "student@test.com",
                StudentName = "Nguyen Student",
                PreferredSlots = new List<string> { "Afternoon" }, // updated
                Status = 1 // Scheduled
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttpAccessor.Object))
            {
                var service = new SemesterService(context);
                var response = await service.EditStudentRegistrationAsync(regId, updateDto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data.Should().NotBeNull();
                response.Data!.PreferredSlots.Should().Contain("Afternoon");
                response.Data.Status.Should().Be(1);
            }
        }

        [Fact]
        public async Task Normal_DeleteStudentRegistrationAsync_WithValidId_ShouldRemoveRegistration()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttpAccessor = GetMockHttpContextAccessor();
            int regId;

            using (var context = new ApplicationDbContext(options, mockHttpAccessor.Object))
            {
                var semester = new Semester { Code = "FALL2026", Name = "Kỳ học Thu 2026", StartDate = DateTime.Now, EndDate = DateTime.Now.AddMonths(3) };
                var course = new Course { Code = "IELTS75", Name = "IELTS Advanced 7.5", Status = 1 };
                var student = new Student { Code = "ST001", Name = "Nguyen Student", Email = "student@test.com", Status = 1 };
                context.Semesters.Add(semester);
                context.Courses.Add(course);
                context.Students.Add(student);
                await context.SaveChangesAsync();

                var reg = new StudentRegistration
                {
                    SemesterId = semester.Id,
                    CourseId = course.Id,
                    StudentId = student.Id,
                    PreferredSlotsJson = "[\"Morning\"]",
                    Status = 0
                };
                context.StudentRegistrations.Add(reg);
                await context.SaveChangesAsync();
                regId = reg.Id;
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttpAccessor.Object))
            {
                var service = new SemesterService(context);
                var response = await service.DeleteStudentRegistrationAsync(regId);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data.Should().BeTrue();

                var exists = await context.StudentRegistrations.AnyAsync(r => r.Id == regId);
                exists.Should().BeFalse();
            }
        }

        #endregion

        #region Boundary Test Cases (Kiểm thử giá trị biên)

        [Fact]
        public async Task Boundary_CreateAsync_WithCodeExactly50Characters_ShouldReturnCreated()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttpAccessor = GetMockHttpContextAccessor();
            var boundaryCode = new string('A', 50); // Độ dài chính xác 50 ký tự (Giới hạn tối đa)

            var newSemesterDto = new SemesterSaveDto
            {
                Code = boundaryCode,
                Name = "Học kỳ Biên Code 50 ký tự",
                StartDate = DateTime.Now,
                EndDate = DateTime.Now.AddMonths(3)
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttpAccessor.Object))
            {
                var service = new SemesterService(context);
                var response = await service.CreateAsync(newSemesterDto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data.Should().NotBeNull();
                response.Data!.Code.Should().HaveLength(50);
            }
        }

        [Fact]
        public async Task Boundary_CreateAsync_WithNameExactly200Characters_ShouldReturnCreated()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttpAccessor = GetMockHttpContextAccessor();
            var boundaryName = new string('B', 200); // Độ dài chính xác 200 ký tự (Giới hạn tối đa)

            var newSemesterDto = new SemesterSaveDto
            {
                Code = "BOUND_NAME",
                Name = boundaryName,
                StartDate = DateTime.Now,
                EndDate = DateTime.Now.AddMonths(3)
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttpAccessor.Object))
            {
                var service = new SemesterService(context);
                var response = await service.CreateAsync(newSemesterDto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data.Should().NotBeNull();
                response.Data!.Name.Should().HaveLength(200);
            }
        }

        #endregion

        #region Abnormal Test Cases (Kiểm thử giá trị bất thường / Lỗi)

        [Fact]
        public async Task Abnormal_GetByIdAsync_WhenIdIsInvalidOrDeleted_ShouldReturnNotFound()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttpAccessor = GetMockHttpContextAccessor();

            // Act
            using (var context = new ApplicationDbContext(options, mockHttpAccessor.Object))
            {
                var service = new SemesterService(context);
                var response = await service.GetByIdAsync(9999); // ID không tồn tại

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
                response.Message.Should().Be("ERR_SEMESTER_NOT_FOUND");
            }
        }

        [Fact]
        public async Task Abnormal_CreateAsync_WithNullOrEmptyCode_ShouldReturnBadRequest()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttpAccessor = GetMockHttpContextAccessor();

            var invalidDto = new SemesterSaveDto
            {
                Code = "   ", // Trống
                Name = "Học kỳ lỗi code",
                StartDate = DateTime.Now,
                EndDate = DateTime.Now.AddMonths(3)
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttpAccessor.Object))
            {
                var service = new SemesterService(context);
                var response = await service.CreateAsync(invalidDto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
                response.Message.Should().Be("ERR_SEMESTER_CODE_NAME_REQUIRED");
            }
        }

        [Fact]
        public async Task Abnormal_CreateAsync_WhenCodeAlreadyExists_ShouldReturnBadRequest()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttpAccessor = GetMockHttpContextAccessor();

            using (var context = new ApplicationDbContext(options, mockHttpAccessor.Object))
            {
                context.Semesters.Add(new Semester 
                { 
                    Code = "EXISTING_CODE", 
                    Name = "Kỳ học đã có sẵn", 
                    StartDate = DateTime.Now, 
                    EndDate = DateTime.Now.AddMonths(3) 
                });
                await context.SaveChangesAsync();
            }

            var duplicateDto = new SemesterSaveDto
            {
                Code = "EXISTING_CODE", // Trùng mã
                Name = "Kỳ học mới trùng mã",
                StartDate = DateTime.Now,
                EndDate = DateTime.Now.AddMonths(3)
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttpAccessor.Object))
            {
                var service = new SemesterService(context);
                var response = await service.CreateAsync(duplicateDto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
                response.Message.Should().Be("ERR_SEMESTER_CODE_EXISTS");
            }
        }

        [Fact]
        public async Task Abnormal_CreateStudentRegistrationAsync_WhenAlreadyRegistered_ShouldReturnBadRequest()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttpAccessor = GetMockHttpContextAccessor();
            int semId;
            int courseId;

            using (var context = new ApplicationDbContext(options, mockHttpAccessor.Object))
            {
                var semester = new Semester { Code = "FALL2026", Name = "Kỳ học Thu 2026", StartDate = DateTime.Now, EndDate = DateTime.Now.AddMonths(3) };
                var course = new Course { Code = "IELTS75", Name = "IELTS Advanced 7.5", Status = 1 };
                var student = new Student { Code = "ST001", Name = "Nguyen Student", Email = "student@test.com", Status = 1 };
                context.Semesters.Add(semester);
                context.Courses.Add(course);
                context.Students.Add(student);
                await context.SaveChangesAsync();

                semId = semester.Id;
                courseId = course.Id;

                var reg = new StudentRegistration
                {
                    SemesterId = semId,
                    CourseId = courseId,
                    StudentId = student.Id,
                    PreferredSlotsJson = "[\"Morning\"]",
                    Status = 0
                };
                context.StudentRegistrations.Add(reg);
                await context.SaveChangesAsync();
            }

            var duplicateDto = new StudentRegistrationSaveDto
            {
                SemesterId = semId,
                CourseId = courseId,
                StudentEmail = "student@test.com",
                StudentName = "Nguyen Student",
                PreferredSlots = new List<string> { "Evening" },
                Status = 0
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttpAccessor.Object))
            {
                var service = new SemesterService(context);
                var response = await service.CreateStudentRegistrationAsync(duplicateDto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
                response.Message.Should().Be("ERR_STUDENT_ALREADY_REGISTERED_FOR_THIS_COURSE");
            }
        }

        [Fact]
        public async Task Abnormal_EditStudentRegistrationAsync_WhenNotFound_ShouldReturnNotFound()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttpAccessor = GetMockHttpContextAccessor();

            var dto = new StudentRegistrationSaveDto
            {
                SemesterId = 1,
                CourseId = 1,
                StudentEmail = "test@test.com",
                StudentName = "Test",
                PreferredSlots = new List<string> { "Morning" },
                Status = 0
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttpAccessor.Object))
            {
                var service = new SemesterService(context);
                var response = await service.EditStudentRegistrationAsync(9999, dto); // ID không tồn tại

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
                response.Message.Should().Be("ERR_REGISTRATION_NOT_FOUND");
            }
        }

        [Fact]
        public async Task Abnormal_DeleteStudentRegistrationAsync_WhenNotFound_ShouldReturnNotFound()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttpAccessor = GetMockHttpContextAccessor();

            // Act
            using (var context = new ApplicationDbContext(options, mockHttpAccessor.Object))
            {
                var service = new SemesterService(context);
                var response = await service.DeleteStudentRegistrationAsync(9999); // ID không tồn tại

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
                response.Message.Should().Be("ERR_REGISTRATION_NOT_FOUND");
            }
        }

        [Fact]
        public async Task Abnormal_EditStudentRegistrationAsync_WhenAlreadyScheduled_ShouldReturnBadRequest()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttpAccessor = GetMockHttpContextAccessor();
            int regId;

            using (var context = new ApplicationDbContext(options, mockHttpAccessor.Object))
            {
                var semester = new Semester { Code = "FALL2026", Name = "Kỳ học Thu 2026", StartDate = DateTime.Now, EndDate = DateTime.Now.AddMonths(3) };
                var course = new Course { Code = "IELTS75", Name = "IELTS Advanced 7.5", Status = 1 };
                var student = new Student { Code = "ST001", Name = "Nguyen Student", Email = "student@test.com", Status = 1 };
                context.Semesters.Add(semester);
                context.Courses.Add(course);
                context.Students.Add(student);
                await context.SaveChangesAsync();

                var reg = new StudentRegistration
                {
                    SemesterId = semester.Id,
                    CourseId = course.Id,
                    StudentId = student.Id,
                    PreferredSlotsJson = "[\"Morning\"]",
                    Status = 1 // Already Scheduled!
                };
                context.StudentRegistrations.Add(reg);
                await context.SaveChangesAsync();
                regId = reg.Id;
            }

            var dto = new StudentRegistrationSaveDto
            {
                SemesterId = 1,
                CourseId = 1,
                StudentEmail = "student@test.com",
                StudentName = "Nguyen Student",
                PreferredSlots = new List<string> { "Afternoon" },
                Status = 1
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttpAccessor.Object))
            {
                var service = new SemesterService(context);
                var response = await service.EditStudentRegistrationAsync(regId, dto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
                response.Message.Should().Be("ERR_REGISTRATION_ALREADY_SCHEDULED_CANNOT_MODIFY");
            }
        }

        [Fact]
        public async Task Abnormal_DeleteStudentRegistrationAsync_WhenAlreadyScheduled_ShouldReturnBadRequest()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttpAccessor = GetMockHttpContextAccessor();
            int regId;

            using (var context = new ApplicationDbContext(options, mockHttpAccessor.Object))
            {
                var semester = new Semester { Code = "FALL2026", Name = "Kỳ học Thu 2026", StartDate = DateTime.Now, EndDate = DateTime.Now.AddMonths(3) };
                var course = new Course { Code = "IELTS75", Name = "IELTS Advanced 7.5", Status = 1 };
                var student = new Student { Code = "ST001", Name = "Nguyen Student", Email = "student@test.com", Status = 1 };
                context.Semesters.Add(semester);
                context.Courses.Add(course);
                context.Students.Add(student);
                await context.SaveChangesAsync();

                var reg = new StudentRegistration
                {
                    SemesterId = semester.Id,
                    CourseId = course.Id,
                    StudentId = student.Id,
                    PreferredSlotsJson = "[\"Morning\"]",
                    Status = 1 // Already Scheduled!
                };
                context.StudentRegistrations.Add(reg);
                await context.SaveChangesAsync();
                regId = reg.Id;
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttpAccessor.Object))
            {
                var service = new SemesterService(context);
                var response = await service.DeleteStudentRegistrationAsync(regId);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
                response.Message.Should().Be("ERR_REGISTRATION_ALREADY_SCHEDULED_CANNOT_DELETE");
            }
        }

        #endregion
    }
}
