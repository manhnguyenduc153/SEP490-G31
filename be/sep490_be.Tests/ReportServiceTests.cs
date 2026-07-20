using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using sep490_be.Models;
using sep490_be.Services.Implementations;
using sep490_be.Enums;

namespace sep490_be.Tests.Services
{
    public class ReportServiceTests
    {
        private DbContextOptions<ApplicationDbContext> CreateNewContextOptions()
        {
            return new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
        }

        [Fact]
        public async Task Normal_GetClassAttendanceSheetAsync_ShouldReturnReportAndCalculations()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = new Mock<IHttpContextAccessor>();
            int classId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var cls = new Class { Code = "C1", Name = "Class 1" };
                context.Classes.Add(cls);
                await context.SaveChangesAsync();
                classId = cls.Id;

                var student = new Student { Code = "ST01", Name = "Alice" };
                context.Students.Add(student);
                await context.SaveChangesAsync();

                context.StudentClasses.Add(new StudentClass { ClassId = classId, StudentId = student.Id });

                var schedule1 = new ClassSchedule { ClassId = classId, LessonNo = 1, ScheduleDate = DateTime.UtcNow };
                var schedule2 = new ClassSchedule { ClassId = classId, LessonNo = 2, ScheduleDate = DateTime.UtcNow.AddDays(2) };
                context.ClassSchedules.AddRange(schedule1, schedule2);
                await context.SaveChangesAsync();

                context.Attendances.Add(new Attendance { ScheduleId = schedule1.Id, StudentId = student.Id, Status = (int)AttendanceStatus.Present });
                context.Attendances.Add(new Attendance { ScheduleId = schedule2.Id, StudentId = student.Id, Status = (int)AttendanceStatus.Absent });
                await context.SaveChangesAsync();
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = new ReportService(context);

                var response = await service.GetClassAttendanceSheetAsync(classId);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                
                var data = response.Data;
                data.Should().NotBeNull();
                data!.TotalSessions.Should().Be(2);
                data.CompletedSessions.Should().Be(2);
                data.AverageAttendanceRate.Should().Be(50);
                
                var studentRow = data.Students.First();
                studentRow.PresentCount.Should().Be(1);
                studentRow.AbsentCount.Should().Be(1);
                studentRow.AttendanceRate.Should().Be(50);
            }
        }
    }
}
