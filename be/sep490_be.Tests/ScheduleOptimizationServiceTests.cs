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
using sep490_be.DTO.Class;
using sep490_be.Enums;
using sep490_be.Models;
using sep490_be.Services.Implementations;

namespace sep490_be.Tests.Services
{
    /// <summary>
    /// Unit test suite for ScheduleOptimizationService.
    /// Code Module: ScheduleOptimizationService
    /// </summary>
    public class ScheduleOptimizationServiceTests
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

        #region Normal Test Cases (Kiểm thử giá trị thông thường)

        [Fact]
        public async Task Normal_CheckConflictAsync_NoConflict_ShouldReturnNoConflict()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                // No existing schedules in db
            }

            var dto = new ClassSaveDto
            {
                Id = 1,
                Code = "CLS01",
                StartDate = DateTime.UtcNow.Date,
                ExpectedLessons = 2,
                TeacherId = 1,
                WeeklySchedules = new List<WeeklyScheduleDto>
                {
                    new WeeklyScheduleDto
                    {
                        DayOfWeek = (int)DateTime.UtcNow.DayOfWeek,
                        StartTime = "08:00",
                        EndTime = "10:00",
                        RoomId = 1
                    }
                }
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = new ScheduleOptimizationService(context);
                var response = await service.CheckConflictAsync(dto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data.Should().NotBeNull();
                response.Data!.HasConflict.Should().BeFalse();
            }
        }

        [Fact]
        public async Task Normal_CheckConflictAsync_WithTeacherConflict_ShouldReturnConflict()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            var conflictDate = DateTime.UtcNow.Date;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var teacher = new Teacher { Id = 1, Code = "T01", Name = "Teacher 1", Status = 1 };
                var otherClass = new Class { Id = 2, Code = "CLS02", Name = "Other Class", Status = 1 };
                var ts = new TimeSlot { Id = 1, Code = "TS01", Name = "Slot 1", StartTime = TimeSpan.Parse("08:00"), EndTime = TimeSpan.Parse("10:00") };
                
                context.Teachers.Add(teacher);
                context.Classes.Add(otherClass);
                context.TimeSlots.Add(ts);
                await context.SaveChangesAsync();

                var schedule = new ClassSchedule
                {
                    ClassId = otherClass.Id,
                    TeacherId = teacher.Id,
                    SlotId = ts.Id,
                    ScheduleDate = conflictDate
                };
                context.ClassSchedules.Add(schedule);
                await context.SaveChangesAsync();
            }

            var dto = new ClassSaveDto
            {
                Id = 1,
                Code = "CLS01",
                StartDate = conflictDate,
                ExpectedLessons = 1,
                TeacherId = 1,
                WeeklySchedules = new List<WeeklyScheduleDto>
                {
                    new WeeklyScheduleDto
                    {
                        DayOfWeek = (int)conflictDate.DayOfWeek,
                        StartTime = "08:00",
                        EndTime = "10:00",
                        RoomId = 2
                    }
                }
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = new ScheduleOptimizationService(context);
                var response = await service.CheckConflictAsync(dto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data.Should().NotBeNull();
                response.Data!.HasConflict.Should().BeTrue();
                response.Data.Conflicts.First().Type.Should().Be("Teacher");
                response.Data.Conflicts.First().ConflictClassCode.Should().Be("CLS02");
            }
        }

        [Fact]
        public async Task Normal_CheckConflictAsync_WithRoomConflict_ShouldReturnConflict()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            var conflictDate = DateTime.UtcNow.Date;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var otherClass = new Class { Id = 2, Code = "CLS02", Name = "Other Class", Status = 1 };
                var ts = new TimeSlot { Id = 1, Code = "TS01", Name = "Slot 1", StartTime = TimeSpan.Parse("08:00"), EndTime = TimeSpan.Parse("10:00") };
                var room = new Room { Id = 1, Name = "Room 101", Status = 1 };

                context.Classes.Add(otherClass);
                context.TimeSlots.Add(ts);
                context.Rooms.Add(room);
                await context.SaveChangesAsync();

                var schedule = new ClassSchedule
                {
                    ClassId = otherClass.Id,
                    RoomId = room.Id,
                    SlotId = ts.Id,
                    ScheduleDate = conflictDate
                };
                context.ClassSchedules.Add(schedule);
                await context.SaveChangesAsync();
            }

            var dto = new ClassSaveDto
            {
                Id = 1,
                Code = "CLS01",
                StartDate = conflictDate,
                ExpectedLessons = 1,
                TeacherId = 2,
                WeeklySchedules = new List<WeeklyScheduleDto>
                {
                    new WeeklyScheduleDto
                    {
                        DayOfWeek = (int)conflictDate.DayOfWeek,
                        StartTime = "08:00",
                        EndTime = "10:00",
                        RoomId = 1
                    }
                }
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = new ScheduleOptimizationService(context);
                var response = await service.CheckConflictAsync(dto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data.Should().NotBeNull();
                response.Data!.HasConflict.Should().BeTrue();
                response.Data.Conflicts.First().Type.Should().Be("Room");
                response.Data.Conflicts.First().ConflictClassCode.Should().Be("CLS02");
            }
        }

        #endregion

        #region Boundary Test Cases (Kiểm thử giá trị biên)

        [Fact]
        public async Task Boundary_AutoScheduleAsync_EmptyClassList_ShouldReturnBadRequest()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = new ScheduleOptimizationService(context);
                var response = await service.AutoScheduleAsync(new List<int>(), new AutoScheduleConstraintDto());

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.Message.Should().Be("ERR_NO_CLASSES_SELECTED");
            }
        }

        #endregion

        #region Abnormal Test Cases (Kiểm thử giá trị bất thường)

        [Fact]
        public async Task Abnormal_CheckConflictAsync_InvalidDayOfWeek_ShouldReturnFail()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            var dto = new ClassSaveDto
            {
                Id = 1,
                Code = "CLS01",
                StartDate = DateTime.UtcNow.Date,
                ExpectedLessons = 1,
                WeeklySchedules = new List<WeeklyScheduleDto>
                {
                    new WeeklyScheduleDto
                    {
                        DayOfWeek = 9, // Invalid DayOfWeek
                        StartTime = "08:00",
                        EndTime = "10:00",
                        RoomId = 1
                    }
                }
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = new ScheduleOptimizationService(context);
                var response = await service.CheckConflictAsync(dto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.Message.Should().Be("ERR_INVALID_DAY_OF_WEEK");
            }
        }

        [Fact]
        public async Task Abnormal_AutoScheduleAsync_NoActiveTeachers_ShouldReturnBadRequest()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var course = new Course { Id = 1, Code = "KH01", Name = "Course 1", Status = 1 };
                var cls = new Class { Id = 1, Code = "C01", Name = "Class 1", CourseId = course.Id, Status = 1, StartDate = DateTime.UtcNow.Date, ExpectedLessons = 30 };
                var student = new Student { Id = 1, Code = "S01", Name = "Student 1", Status = 1 };
                var sc = new StudentClass { ClassId = cls.Id, StudentId = student.Id, Status = 1 };
                
                context.Courses.Add(course);
                context.Classes.Add(cls);
                context.Students.Add(student);
                context.StudentClasses.Add(sc);
                await context.SaveChangesAsync();
                
                // Active rooms exists, but NO active teachers are created.
                var room = new Room { Id = 1, Name = "Room 1", Capacity = 20, Status = 1 };
                context.Rooms.Add(room);
                await context.SaveChangesAsync();
            }

            var constraints = new AutoScheduleConstraintDto();

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = new ScheduleOptimizationService(context);
                var response = await service.AutoScheduleAsync(new List<int> { 1 }, constraints);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.Message.Should().Be("ERR_NO_ACTIVE_TEACHERS");
            }
        }

        [Fact]
        public async Task Abnormal_AutoScheduleAsync_NoActiveRooms_ShouldReturnBadRequest()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var course = new Course { Id = 1, Code = "KH01", Name = "Course 1", Status = 1 };
                var cls = new Class { Id = 1, Code = "C01", Name = "Class 1", CourseId = course.Id, Status = 1, StartDate = DateTime.UtcNow.Date, ExpectedLessons = 30 };
                var student = new Student { Id = 1, Code = "S01", Name = "Student 1", Status = 1 };
                var sc = new StudentClass { ClassId = cls.Id, StudentId = student.Id, Status = 1 };
                var teacher = new Teacher { Id = 1, Code = "T01", Name = "Teacher 1", Status = 1 };

                context.Courses.Add(course);
                context.Classes.Add(cls);
                context.Students.Add(student);
                context.StudentClasses.Add(sc);
                context.Teachers.Add(teacher);
                await context.SaveChangesAsync();

                // NO active rooms are created.
            }

            var constraints = new AutoScheduleConstraintDto();

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = new ScheduleOptimizationService(context);
                var response = await service.AutoScheduleAsync(new List<int> { 1 }, constraints);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.Message.Should().Be("ERR_NO_ACTIVE_ROOMS");
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
