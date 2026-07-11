using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using sep490_be.DTO;
using sep490_be.DTO.Attendance;
using sep490_be.Models;
using sep490_be.Repositories.Common;
using sep490_be.Repositories.Implementations;
using sep490_be.Services.Implementations;

namespace sep490_be.Tests.Services
{
    /// <summary>
    /// Unit test suite for AttendanceService.
    /// Code Module: AttendanceService
    /// </summary>
    public class AttendanceServiceTests
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
        public async Task Normal_GetByScheduleIdAsync_WithExistingAttendances_ShouldReturnList()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int scheduleId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var schedule = new ClassSchedule { LessonNo = 1, ScheduleDate = DateTime.UtcNow.Date };
                context.ClassSchedules.Add(schedule);
                await context.SaveChangesAsync();
                scheduleId = schedule.Id;

                var student = new Student { Code = "ST01", Name = "Student 1", Email = "s1@test.com", Status = 1 };
                context.Students.Add(student);
                await context.SaveChangesAsync();

                var attendance = new Attendance
                {
                    ScheduleId = scheduleId,
                    StudentId = student.Id,
                    Status = 1, // Present
                    CheckInTime = DateTime.UtcNow,
                    Description = "Checked in on time"
                };
                context.Attendances.Add(attendance);
                await context.SaveChangesAsync();
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var attRepo = new AttendanceRepository(context, uow);
                var service = new AttendanceService(attRepo, context);

                var response = await service.GetByScheduleIdAsync(scheduleId);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data.Should().NotBeNull();
                response.Data.Should().HaveCount(1);
                response.Data.First().StudentCode.Should().Be("ST01");
                response.Data.First().Description.Should().Be("Checked in on time");
            }
        }

        [Fact]
        public async Task Normal_GetByScheduleIdAsync_WithNoExistingAttendances_ShouldReturnDefaultEnrolledStudents()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int scheduleId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var cls = new Class { Code = "CLS01", Name = "Math Class", Status = 1 };
                context.Classes.Add(cls);
                await context.SaveChangesAsync();

                var student = new Student { Code = "ST01", Name = "Student 1", Email = "s1@test.com", Status = 1 };
                context.Students.Add(student);
                await context.SaveChangesAsync();

                context.StudentClasses.Add(new StudentClass { ClassId = cls.Id, StudentId = student.Id, Status = 1 });

                var schedule = new ClassSchedule { ClassId = cls.Id, LessonNo = 1, ScheduleDate = DateTime.UtcNow.Date };
                context.ClassSchedules.Add(schedule);
                await context.SaveChangesAsync();
                scheduleId = schedule.Id;
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var attRepo = new AttendanceRepository(context, uow);
                var service = new AttendanceService(attRepo, context);

                var response = await service.GetByScheduleIdAsync(scheduleId);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data.Should().NotBeNull();
                response.Data.Should().HaveCount(1);
                response.Data.First().StudentCode.Should().Be("ST01");
                response.Data.First().Status.Should().Be(1); // Present (Default)
            }
        }

        [Fact]
        public async Task Normal_BulkSaveAsync_ShouldSaveNewAndUpdatedAttendances()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int scheduleId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var schedule = new ClassSchedule { LessonNo = 1, ScheduleDate = DateTime.UtcNow.Date };
                context.ClassSchedules.Add(schedule);
                await context.SaveChangesAsync();
                scheduleId = schedule.Id;

                var student1 = new Student { Code = "ST01", Name = "Student 1", Email = "s1@test.com", Status = 1 };
                var student2 = new Student { Code = "ST02", Name = "Student 2", Email = "s2@test.com", Status = 1 };
                context.Students.AddRange(student1, student2);
                await context.SaveChangesAsync();

                // Existing attendance for student 1
                var att = new Attendance { ScheduleId = scheduleId, StudentId = student1.Id, Status = 1 };
                context.Attendances.Add(att);
                await context.SaveChangesAsync();
            }

            var saveDto = new AttendanceBulkSaveDto
            {
                ScheduleId = scheduleId,
                Attendances = new List<AttendanceStudentSaveDto>
                {
                    new AttendanceStudentSaveDto { StudentId = 1, Status = 2, Description = "Late" }, // Update status to Absent/Late
                    new AttendanceStudentSaveDto { StudentId = 2, Status = 1, Description = "Present" } // Add new attendance
                }
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var attRepo = new AttendanceRepository(context, uow);
                var service = new AttendanceService(attRepo, context);

                var response = await service.BulkSaveAsync(saveDto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();

                var list = await context.Attendances.Where(a => a.ScheduleId == scheduleId).ToListAsync();
                list.Should().HaveCount(2);
                list.FirstOrDefault(a => a.StudentId == 1)!.Status.Should().Be(2);
                list.FirstOrDefault(a => a.StudentId == 1)!.Description.Should().Be("Late");
                list.FirstOrDefault(a => a.StudentId == 2)!.Status.Should().Be(1);
            }
        }

        [Fact]
        public async Task Normal_GetReportByClassIdAsync_ShouldReturnReport()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int classId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var cls = new Class { Code = "CLS01", Name = "Math Class", Status = 1 };
                context.Classes.Add(cls);
                await context.SaveChangesAsync();
                classId = cls.Id;

                var student = new Student { Code = "ST01", Name = "Student 1", Email = "s1@test.com", Status = 1 };
                context.Students.Add(student);
                await context.SaveChangesAsync();

                context.StudentClasses.Add(new StudentClass { ClassId = classId, StudentId = student.Id, Status = 1 });

                var schedule = new ClassSchedule { ClassId = classId, LessonNo = 1, ScheduleDate = DateTime.UtcNow.Date };
                context.ClassSchedules.Add(schedule);
                await context.SaveChangesAsync();

                context.Attendances.Add(new Attendance { ScheduleId = schedule.Id, StudentId = student.Id, Status = 1 });
                await context.SaveChangesAsync();
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var attRepo = new AttendanceRepository(context, uow);
                var service = new AttendanceService(attRepo, context);

                var response = await service.GetReportByClassIdAsync(classId);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data.Should().NotBeNull();
                response.Data!.Students.Should().HaveCount(1);
                response.Data.Sessions.Should().HaveCount(1);
                response.Data.Students.First().Attendances.First().Status.Should().Be(1);
            }
        }

        #endregion

        #region Boundary Test Cases (Kiểm thử giá trị biên)

        [Fact]
        public async Task Boundary_BulkSaveAsync_EmptyList_ShouldSucceed()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int scheduleId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var schedule = new ClassSchedule { LessonNo = 1, ScheduleDate = DateTime.UtcNow.Date };
                context.ClassSchedules.Add(schedule);
                await context.SaveChangesAsync();
                scheduleId = schedule.Id;
            }

            var saveDto = new AttendanceBulkSaveDto
            {
                ScheduleId = scheduleId,
                Attendances = new List<AttendanceStudentSaveDto>()
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var attRepo = new AttendanceRepository(context, uow);
                var service = new AttendanceService(attRepo, context);

                var response = await service.BulkSaveAsync(saveDto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
            }
        }

        #endregion

        #region Abnormal Test Cases (Kiểm thử giá trị bất thường)

        [Fact]
        public async Task Abnormal_GetByScheduleIdAsync_ScheduleNotFound_ShouldReturnNotFound()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var attRepo = new AttendanceRepository(context, uow);
                var service = new AttendanceService(attRepo, context);

                var response = await service.GetByScheduleIdAsync(9999);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.Message.Should().Be("ERR_SCHEDULE_NOT_FOUND");
            }
        }

        [Fact]
        public async Task Abnormal_BulkSaveAsync_ScheduleNotFound_ShouldReturnNotFound()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            var saveDto = new AttendanceBulkSaveDto
            {
                ScheduleId = 9999,
                Attendances = new List<AttendanceStudentSaveDto>()
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var attRepo = new AttendanceRepository(context, uow);
                var service = new AttendanceService(attRepo, context);

                var response = await service.BulkSaveAsync(saveDto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.Message.Should().Be("ERR_SCHEDULE_NOT_FOUND");
            }
        }

        [Fact]
        public async Task Abnormal_GetReportByClassIdAsync_ClassNotFound_ShouldReturnNotFound()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var attRepo = new AttendanceRepository(context, uow);
                var service = new AttendanceService(attRepo, context);

                var response = await service.GetReportByClassIdAsync(9999);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.Message.Should().Be("ERR_CLASS_NOT_FOUND");
            }
        }

        [Fact]
        public async Task Abnormal_IntentionalFailure_ShouldFail()
        {
            // Arrange
            // This test case is written to fail intentionally to verify that the unit test runner
            // captures failing assertions correctly, as requested by the user.
            var value = true;

            // Assert
            value.Should().BeFalse("This is an intentional failure to prove that the test suite is capable of identifying failures.");
        }

        #endregion
    }
}
