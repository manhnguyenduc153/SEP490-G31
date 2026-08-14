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
using sep490_be.Repositories.Common;
using sep490_be.Repositories.Implementations;
using sep490_be.Services.Implementations;
using sep490_be.Services.Interfaces;

namespace sep490_be.Tests.Services
{
    /// <summary>
    /// Unit test suite for ScheduleOptimizationService.
    /// Code Module: ScheduleOptimizationService
    /// </summary>
    public class ScheduleOptimizationServiceTests
    {
        private sealed class ScheduleOptimizationService : sep490_be.Services.Implementations.ScheduleOptimizationService
        {
            public ScheduleOptimizationService(ApplicationDbContext context)
                : base(
                    new ClassRepository(context, new UnitOfWork<ApplicationDbContext>(context)),
                    new BaseRepository<ClassSchedule, ApplicationDbContext>(context, new UnitOfWork<ApplicationDbContext>(context)),
                    new BaseRepository<TeacherAvailability, ApplicationDbContext>(context, new UnitOfWork<ApplicationDbContext>(context)),
                    new TeacherRepository(context, new UnitOfWork<ApplicationDbContext>(context)),
                    new RoomRepository(context, new UnitOfWork<ApplicationDbContext>(context)),
                    new SemesterRepository(context, new UnitOfWork<ApplicationDbContext>(context)),
                    new StudentRegistrationRepository(context, new UnitOfWork<ApplicationDbContext>(context)),
                    new BaseRepository<StudentClass, ApplicationDbContext>(context, new UnitOfWork<ApplicationDbContext>(context)),
                    new BaseRepository<TimeSlot, ApplicationDbContext>(context, new UnitOfWork<ApplicationDbContext>(context)),
                    new BaseRepository<ScheduleVersion, ApplicationDbContext>(context, new UnitOfWork<ApplicationDbContext>(context)),
                    CreateNoOpNotificationService())
            {
            }

            private static INotificationService CreateNoOpNotificationService()
            {
                var mock = new Mock<INotificationService>();
                mock.Setup(n => n.SendClassCreatedNotificationAsync(It.IsAny<Class>())).Returns(Task.CompletedTask);
                return mock.Object;
            }
        }

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

        private async Task<Semester> SeedSemesterSchedulingScenarioAsync(
            ApplicationDbContext context,
            int studentCount = 1,
            bool addTeacher = true,
            bool addRoom = true,
            int roomCapacity = 30,
            string preferredSlotsJson = "[\"morning\"]")
        {
            var semester = new Semester
            {
                Code = $"SEM-{Guid.NewGuid():N}",
                Name = "Semester for auto scheduling",
                StartDate = DateTime.UtcNow.Date.AddDays(1),
                EndDate = DateTime.UtcNow.Date.AddDays(14),
                Status = (int)SemesterStatus.Active
            };
            var course = new Course
            {
                Code = $"COURSE-{Guid.NewGuid():N}",
                Name = "Course for auto scheduling",
                Duration = 4,
                Status = 1
            };

            context.AddRange(semester, course);
            if (addTeacher)
            {
                context.Teachers.Add(new Teacher
                {
                    Code = $"T-{Guid.NewGuid():N}",
                    Name = "Active Teacher",
                    Status = (int)TeacherStatus.Active
                });
            }
            if (addRoom)
            {
                context.Rooms.Add(new Room
                {
                    Name = "Active Room",
                    Capacity = roomCapacity,
                    Status = (int)RoomStatus.Active
                });
            }

            for (var index = 1; index <= studentCount; index++)
            {
                var student = new Student
                {
                    Code = $"ST-{Guid.NewGuid():N}",
                    Name = $"Student {index}",
                    Status = 1
                };
                context.Students.Add(student);
                context.StudentRegistrations.Add(new StudentRegistration
                {
                    Student = student,
                    Course = course,
                    Semester = semester,
                    PreferredSlotsJson = preferredSlotsJson,
                    Status = (int)StudentRegistrationStatus.Pending
                });
            }

            await context.SaveChangesAsync();
            return semester;
        }

        private async Task<Class> SeedClassSchedulingScenarioAsync(ApplicationDbContext context)
        {
            var course = new Course
            {
                Code = $"COURSE-{Guid.NewGuid():N}",
                Name = "Course for class scheduling",
                Duration = 4,
                Status = 1
            };
            var targetClass = new Class
            {
                Code = $"CLASS-{Guid.NewGuid():N}",
                Name = "Class for auto scheduling",
                Course = course,
                StartDate = DateTime.UtcNow.Date.AddDays(1),
                ExpectedLessons = 1,
                Status = (int)ClassStatus.Planning
            };
            var teacher = new Teacher
            {
                Code = $"T-{Guid.NewGuid():N}",
                Name = "Active Teacher",
                Status = (int)TeacherStatus.Active
            };
            var room = new Room
            {
                Name = "Active Room",
                Capacity = 30,
                Status = (int)RoomStatus.Active
            };
            var student = new Student
            {
                Code = $"ST-{Guid.NewGuid():N}",
                Name = "Enrolled Student",
                Status = 1
            };

            context.AddRange(targetClass, teacher, room, student);
            await context.SaveChangesAsync();
            context.StudentClasses.Add(new StudentClass
            {
                ClassId = targetClass.Id,
                StudentId = student.Id,
                Status = (int)StudentClassStatus.Enrolled
            });
            await context.SaveChangesAsync();
            return targetClass;
        }

        /// <summary>
        /// Seeds a semester with one pending registration and returns a ready-to-submit
        /// SaveScheduleDraftRequestDto for it (one class, one weekly session on the semester's
        /// start-date weekday, starting at 08:00 — used as a distinguishing marker in version tests).
        /// </summary>
        private async Task<(Semester semester, SaveScheduleDraftRequestDto request)> SeedDraftReadyScenarioAsync(ApplicationDbContext context)
        {
            var semester = await SeedSemesterSchedulingScenarioAsync(context, studentCount: 1);
            var course = await context.Courses.SingleAsync();
            var teacher = await context.Teachers.SingleAsync();
            var student = await context.Students.SingleAsync();

            var request = new SaveScheduleDraftRequestDto
            {
                SemesterId = semester.Id,
                Classes = new List<ClassDraftSaveDto>
                {
                    new ClassDraftSaveDto
                    {
                        Code = $"CLS-{Guid.NewGuid():N}",
                        Name = "Draft Class",
                        CourseId = course.Id,
                        TeacherId = teacher.Id,
                        ExpectedLessons = 1,
                        WeeklySchedules = new List<WeeklyScheduleDto>
                        {
                            new() { DayOfWeek = (int)semester.StartDate.DayOfWeek, StartTime = "08:00", EndTime = "10:00" }
                        },
                        Students = new List<StudentEnrollDto> { new() { StudentId = student.Id, EnrollType = 0 } }
                    }
                }
            };

            return (semester, request);
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

        [Fact]
        public async Task CheckConflictAsync_WhenStartDateIsNull_ShouldReturnNoScheduleData()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var dto = new ClassSaveDto
            {
                StartDate = null,
                ExpectedLessons = 1,
                WeeklySchedules = new List<WeeklyScheduleDto>
                {
                    new() { DayOfWeek = 1, StartTime = "08:00", EndTime = "10:00", RoomId = 1 }
                }
            };

            var response = await new ScheduleOptimizationService(context).CheckConflictAsync(dto);

            response.Success.Should().BeTrue();
            response.Message.Should().Be("NO_SCHEDULE_DATA_TO_CHECK");
            response.Data!.HasConflict.Should().BeFalse();
        }

        [Fact]
        public async Task CheckConflictAsync_WhenExpectedLessonsIsZero_ShouldReturnNoScheduleData()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var dto = new ClassSaveDto
            {
                StartDate = DateTime.UtcNow.Date,
                ExpectedLessons = 0,
                WeeklySchedules = new List<WeeklyScheduleDto>
                {
                    new() { DayOfWeek = 1, StartTime = "08:00", EndTime = "10:00", RoomId = 1 }
                }
            };

            var response = await new ScheduleOptimizationService(context).CheckConflictAsync(dto);

            response.Success.Should().BeTrue();
            response.Message.Should().Be("NO_SCHEDULE_DATA_TO_CHECK");
            response.Data!.HasConflict.Should().BeFalse();
        }

        [Fact]
        public async Task CheckConflictAsync_WhenTimesAreInvalid_ShouldReturnNoProposedSchedules()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var dto = new ClassSaveDto
            {
                StartDate = DateTime.UtcNow.Date,
                ExpectedLessons = 1,
                WeeklySchedules = new List<WeeklyScheduleDto>
                {
                    new() { DayOfWeek = 1, StartTime = "invalid", EndTime = "invalid", RoomId = 1 }
                }
            };

            var response = await new ScheduleOptimizationService(context).CheckConflictAsync(dto);

            response.Success.Should().BeTrue();
            response.Message.Should().Be("NO_PROPOSED_SCHEDULES_GENERATED");
            response.Data!.HasConflict.Should().BeFalse();
        }

        [Fact]
        public async Task CheckConflictAsync_WhenDatabaseThrows_ShouldReturnInternalServerError()
        {
            var options = CreateNewContextOptions();
            var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var service = new ScheduleOptimizationService(context);
            context.Dispose();
            var dto = new ClassSaveDto
            {
                Id = 1,
                StartDate = DateTime.UtcNow.Date,
                ExpectedLessons = 1,
                TeacherId = 1,
                WeeklySchedules = new List<WeeklyScheduleDto>
                {
                    new()
                    {
                        DayOfWeek = (int)DateTime.UtcNow.DayOfWeek,
                        StartTime = "08:00",
                        EndTime = "10:00",
                        RoomId = 1
                    }
                }
            };

            var response = await service.CheckConflictAsync(dto);

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
            response.Message.Should().NotBeNullOrWhiteSpace();
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
        public async Task CheckConflictAsync_WithoutScheduleData_ShouldReturnNoScheduleData()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);

            var response = await new ScheduleOptimizationService(context).CheckConflictAsync(new ClassSaveDto
            {
                StartDate = DateTime.UtcNow.Date,
                ExpectedLessons = 2,
                WeeklySchedules = new List<WeeklyScheduleDto>()
            });

            response.Success.Should().BeTrue();
            response.Message.Should().Be("NO_SCHEDULE_DATA_TO_CHECK");
            response.Data!.HasConflict.Should().BeFalse();
        }

        [Fact]
        public async Task CheckConflictAsync_WhenExistingScheduleEndsAtProposedStart_ShouldNotConflict()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var date = DateTime.UtcNow.Date;
            var teacher = new Teacher { Code = "T-EDGE", Name = "Edge Teacher", Status = 1 };
            var existingClass = new Class { Code = "C-EDGE", Name = "Edge Class", Status = 1 };
            var slot = new TimeSlot
            {
                Code = "S-EDGE",
                Name = "Earlier",
                StartTime = TimeSpan.Parse("08:00"),
                EndTime = TimeSpan.Parse("10:00")
            };
            context.AddRange(teacher, existingClass, slot);
            await context.SaveChangesAsync();
            context.ClassSchedules.Add(new ClassSchedule
            {
                ClassId = existingClass.Id,
                TeacherId = teacher.Id,
                SlotId = slot.Id,
                ScheduleDate = date
            });
            await context.SaveChangesAsync();

            var response = await new ScheduleOptimizationService(context).CheckConflictAsync(new ClassSaveDto
            {
                Id = 999,
                StartDate = date,
                ExpectedLessons = 1,
                TeacherId = teacher.Id,
                WeeklySchedules = new List<WeeklyScheduleDto>
                {
                    new() { DayOfWeek = (int)date.DayOfWeek, StartTime = "10:00", EndTime = "12:00" }
                }
            });

            response.Success.Should().BeTrue();
            response.Data!.HasConflict.Should().BeFalse();
        }

        [Fact]
        public async Task CheckConflictAsync_ShouldIgnoreSchedulesFromSameClass()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var date = DateTime.UtcNow.Date;
            var teacher = new Teacher { Code = "T-SELF", Name = "Self Teacher", Status = 1 };
            var currentClass = new Class { Code = "C-SELF", Name = "Current Class", Status = 1 };
            var slot = new TimeSlot
            {
                Code = "S-SELF",
                Name = "Same Slot",
                StartTime = TimeSpan.Parse("08:00"),
                EndTime = TimeSpan.Parse("10:00")
            };
            context.AddRange(teacher, currentClass, slot);
            await context.SaveChangesAsync();
            context.ClassSchedules.Add(new ClassSchedule
            {
                ClassId = currentClass.Id,
                TeacherId = teacher.Id,
                SlotId = slot.Id,
                ScheduleDate = date
            });
            await context.SaveChangesAsync();

            var response = await new ScheduleOptimizationService(context).CheckConflictAsync(new ClassSaveDto
            {
                Id = currentClass.Id,
                StartDate = date,
                ExpectedLessons = 1,
                TeacherId = teacher.Id,
                WeeklySchedules = new List<WeeklyScheduleDto>
                {
                    new() { DayOfWeek = (int)date.DayOfWeek, StartTime = "08:00", EndTime = "10:00" }
                }
            });

            response.Success.Should().BeTrue();
            response.Data!.HasConflict.Should().BeFalse();
        }

        [Fact]
        public async Task CheckConflictAsync_WhenTeacherAndRoomBothConflict_ShouldReturnBothDetails()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var date = DateTime.UtcNow.Date;
            var teacher = new Teacher { Code = "T-BOTH", Name = "Both Teacher", Status = 1 };
            var room = new Room { Name = "Both Room", Status = 1 };
            var existingClass = new Class { Code = "C-BOTH", Name = "Both Class", Status = 1 };
            var slot = new TimeSlot
            {
                Code = "S-BOTH",
                Name = "Overlap",
                StartTime = TimeSpan.Parse("08:00"),
                EndTime = TimeSpan.Parse("10:00")
            };
            context.AddRange(teacher, room, existingClass, slot);
            await context.SaveChangesAsync();
            context.ClassSchedules.Add(new ClassSchedule
            {
                ClassId = existingClass.Id,
                TeacherId = teacher.Id,
                RoomId = room.Id,
                SlotId = slot.Id,
                ScheduleDate = date
            });
            await context.SaveChangesAsync();

            var response = await new ScheduleOptimizationService(context).CheckConflictAsync(new ClassSaveDto
            {
                Id = 999,
                StartDate = date,
                ExpectedLessons = 1,
                TeacherId = teacher.Id,
                WeeklySchedules = new List<WeeklyScheduleDto>
                {
                    new()
                    {
                        DayOfWeek = (int)date.DayOfWeek,
                        StartTime = "08:30",
                        EndTime = "09:30",
                        RoomId = room.Id
                    }
                }
            });

            response.Success.Should().BeTrue();
            response.Data!.HasConflict.Should().BeTrue();
            response.Data.Conflicts.Select(x => x.Type).Should().BeEquivalentTo("Teacher", "Room");
        }

        [Fact]
        public async Task AutoScheduleAsync_WhenClassesDoNotExist_ShouldReturnNotFound()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);

            var response = await new ScheduleOptimizationService(context)
                .AutoScheduleAsync(new List<int> { 9999 }, new AutoScheduleConstraintDto());

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
            response.Message.Should().Be("ERR_CLASSES_NOT_FOUND");
        }

        [Fact]
        public async Task AutoScheduleAsync_WhenAllClassesAlreadyScheduled_ShouldReturnBadRequest()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var course = new Course { Code = "COURSE-S", Name = "Scheduled Course", Status = 1 };
            var scheduledClass = new Class
            {
                Code = "CLASS-S",
                Name = "Scheduled Class",
                Course = course,
                Status = 1,
                WeeklySchedulesJson = "[{\"dayOfWeek\":1,\"startTime\":\"08:00\",\"endTime\":\"10:00\"}]"
            };
            context.Classes.Add(scheduledClass);
            await context.SaveChangesAsync();

            var response = await new ScheduleOptimizationService(context)
                .AutoScheduleAsync(new List<int> { scheduledClass.Id }, new AutoScheduleConstraintDto());

            response.Success.Should().BeFalse();
            response.Message.Should().Be("ERR_ALL_CLASSES_ALREADY_SCHEDULED");
        }

        [Fact]
        public async Task AutoScheduleAsync_WhenClassHasNoCourse_ShouldReturnClassSpecificError()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var student = new Student { Code = "ST-NC", Name = "No Course Student", Status = 1 };
            var targetClass = new Class { Code = "CLASS-NC", Name = "No Course", Status = 1 };
            context.AddRange(student, targetClass);
            await context.SaveChangesAsync();
            context.StudentClasses.Add(new StudentClass { ClassId = targetClass.Id, StudentId = student.Id, Status = 1 });
            await context.SaveChangesAsync();

            var response = await new ScheduleOptimizationService(context)
                .AutoScheduleAsync(new List<int> { targetClass.Id }, new AutoScheduleConstraintDto());

            response.Success.Should().BeFalse();
            response.Message.Should().Be("ERR_CLASS_NO_COURSE_CLASS-NC");
        }

        [Fact]
        public async Task AutoScheduleAsync_WhenClassHasNoStudents_ShouldReturnClassSpecificError()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var course = new Course { Code = "COURSE-NS", Name = "No Students Course", Status = 1 };
            var targetClass = new Class { Code = "CLASS-NS", Name = "No Students", Course = course, Status = 1 };
            context.Classes.Add(targetClass);
            await context.SaveChangesAsync();

            var response = await new ScheduleOptimizationService(context)
                .AutoScheduleAsync(new List<int> { targetClass.Id }, new AutoScheduleConstraintDto());

            response.Success.Should().BeFalse();
            response.Message.Should().Be("ERR_CLASS_NO_STUDENTS_CLASS-NS");
        }

        [Fact]
        public async Task AutoScheduleAsync_WhenStudentsExceedEveryRoomCapacity_ShouldReturnBadRequest()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var course = new Course { Code = "COURSE-CAP", Name = "Capacity Course", Status = 1 };
            var targetClass = new Class { Code = "CLASS-CAP", Name = "Capacity Class", Course = course, Status = 1 };
            var teacher = new Teacher { Code = "T-CAP", Name = "Capacity Teacher", Status = 1 };
            var room = new Room { Name = "Tiny Room", Capacity = 1, Status = 1 };
            var first = new Student { Code = "ST-CAP-1", Name = "First", Status = 1 };
            var second = new Student { Code = "ST-CAP-2", Name = "Second", Status = 1 };
            context.AddRange(targetClass, teacher, room, first, second);
            await context.SaveChangesAsync();
            context.StudentClasses.AddRange(
                new StudentClass { ClassId = targetClass.Id, StudentId = first.Id, Status = 1 },
                new StudentClass { ClassId = targetClass.Id, StudentId = second.Id, Status = 1 });
            await context.SaveChangesAsync();

            var response = await new ScheduleOptimizationService(context)
                .AutoScheduleAsync(new List<int> { targetClass.Id }, new AutoScheduleConstraintDto());

            response.Success.Should().BeFalse();
            response.Message.Should().Be("ERR_CLASS_STUDENTS_EXCEED_ROOM_CAPACITY_CLASS-CAP");
        }

        [Fact]
        public async Task AutoScheduleAsync_WhenNoFeasibleScheduleExists_ShouldReturnConflict()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var targetClass = await SeedClassSchedulingScenarioAsync(context);
            var teacher = await context.Teachers.SingleAsync();
            var room = await context.Rooms.SingleAsync();
            var occupiedClass = new Class
            {
                Code = "CLASS-OCCUPIED",
                Name = "Occupied Class",
                Status = (int)ClassStatus.Planning
            };
            var eveningSlot = new TimeSlot
            {
                Code = "TS-1830-2030",
                Name = "Evening",
                StartTime = new TimeSpan(18, 30, 0),
                EndTime = new TimeSpan(20, 30, 0)
            };
            context.AddRange(occupiedClass, eveningSlot);
            await context.SaveChangesAsync();

            var firstDate = targetClass.StartDate!.Value;
            for (var date = firstDate; date <= firstDate.AddDays(7); date = date.AddDays(1))
            {
                if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
                context.ClassSchedules.Add(new ClassSchedule
                {
                    ClassId = occupiedClass.Id,
                    TeacherId = teacher.Id,
                    RoomId = room.Id,
                    SlotId = eveningSlot.Id,
                    ScheduleDate = date,
                    Status = (int)ClassScheduleStatus.Scheduled
                });
            }
            await context.SaveChangesAsync();

            var response = await new ScheduleOptimizationService(context).AutoScheduleAsync(
                new List<int> { targetClass.Id },
                new AutoScheduleConstraintDto
                {
                    SessionsPerWeek = 1,
                    TimePreferences = new List<string> { "evening" }
                });

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
            response.Message.Should().Be("ERR_NO_FEASIBLE_SCHEDULE_FOUND");
        }

        [Fact]
        public async Task AutoScheduleAsync_WithValidData_ShouldCreateSchedules()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var targetClass = await SeedClassSchedulingScenarioAsync(context);

            var response = await new ScheduleOptimizationService(context).AutoScheduleAsync(
                new List<int> { targetClass.Id },
                new AutoScheduleConstraintDto
                {
                    SessionsPerWeek = 1,
                    TimePreferences = new List<string> { "morning" }
                });

            response.Success.Should().BeTrue();
            response.StatusCode.Should().Be(StatusCodes.Status200OK);
            response.Message.Should().Be("AUTO_SCHEDULING_COMPLETED");
            response.Data.Should().ContainSingle();
            var savedClass = await context.Classes
                .Include(entity => entity.ClassSchedules)
                .SingleAsync(entity => entity.Id == targetClass.Id);
            savedClass.TeacherId.Should().NotBeNull();
            savedClass.WeeklySchedulesJson.Should().NotBeNullOrWhiteSpace();
            savedClass.ClassSchedules.Should().NotBeEmpty();
        }

        [Fact]
        public async Task AutoScheduleAsync_WhenPersistenceFails_ShouldReturnInternalServerError()
        {
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int classId;
            using (var seedContext = new ApplicationDbContext(options, mockHttp.Object))
            {
                classId = (await SeedClassSchedulingScenarioAsync(seedContext)).Id;
            }

            var mockContext = new Mock<ApplicationDbContext>(options, mockHttp.Object)
            {
                CallBase = true
            };
            mockContext
                .Setup(context => context.SaveChangesAsync(It.IsAny<System.Threading.CancellationToken>()))
                .ThrowsAsync(new DbUpdateException("Database Exception"));

            var response = await new ScheduleOptimizationService(mockContext.Object).AutoScheduleAsync(
                new List<int> { classId },
                new AutoScheduleConstraintDto
                {
                    SessionsPerWeek = 1,
                    TimePreferences = new List<string> { "morning" }
                });

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
            response.Message.Should().Contain("Error writing auto scheduling results");
            response.Message.Should().Contain("Database Exception");
            mockContext.Object.Dispose();
        }

        [Fact]
        public async Task AutoScheduleSemesterAsync_WithNullRequest_ShouldReturnBadRequest()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);

            var response = await new ScheduleOptimizationService(context).AutoScheduleSemesterAsync(null!);

            response.Success.Should().BeFalse();
            response.Message.Should().Be("ERR_INVALID_REQUEST");
        }

        [Fact]
        public async Task AutoScheduleSemesterAsync_WhenSemesterDoesNotExist_ShouldReturnNotFound()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);

            var response = await new ScheduleOptimizationService(context).AutoScheduleSemesterAsync(
                new AutoScheduleSemesterRequestDto { SemesterId = 9999 });

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
            response.Message.Should().Be("ERR_SEMESTER_NOT_FOUND");
        }

        [Fact]
        public async Task AutoScheduleSemesterAsync_WhenNoPendingRegistrations_ShouldReturnBadRequest()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var semester = new Semester
            {
                Code = "SEM-EMPTY",
                Name = "Empty Semester",
                StartDate = DateTime.UtcNow.Date.AddDays(1),
                EndDate = DateTime.UtcNow.Date.AddMonths(3),
                Status = 1
            };
            context.Semesters.Add(semester);
            await context.SaveChangesAsync();

            var response = await new ScheduleOptimizationService(context).AutoScheduleSemesterAsync(
                new AutoScheduleSemesterRequestDto { SemesterId = semester.Id });

            response.Success.Should().BeFalse();
            response.Message.Should().Be("ERR_NO_PENDING_REGISTRATIONS_FOUND");
        }

        [Fact]
        public async Task AutoScheduleSemesterAsync_WhenNoDraftClassCanBeGenerated_ShouldReturnBadRequest()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var semester = await SeedSemesterSchedulingScenarioAsync(context);

            var response = await new ScheduleOptimizationService(context).AutoScheduleSemesterAsync(
                new AutoScheduleSemesterRequestDto
                {
                    SemesterId = semester.Id,
                    MinClassSize = 2,
                    MaxClassSize = 15
                });

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
            response.Message.Should().Be("ERR_NO_DRAFT_CLASSES_GENERATED");
            context.Classes.Should().BeEmpty();
        }

        [Fact]
        public async Task AutoScheduleSemesterAsync_WhenNoActiveTeacher_ShouldReturnBadRequest()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var semester = await SeedSemesterSchedulingScenarioAsync(context, addTeacher: false);

            var response = await new ScheduleOptimizationService(context).AutoScheduleSemesterAsync(
                new AutoScheduleSemesterRequestDto
                {
                    SemesterId = semester.Id,
                    MinClassSize = 1,
                    Constraints = new AutoScheduleConstraintDto { SessionsPerWeek = 1 }
                });

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
            response.Message.Should().Be("ERR_NO_ACTIVE_TEACHERS");
            context.Classes.Should().BeEmpty();
        }

        [Fact]
        public async Task AutoScheduleSemesterAsync_WhenNoActiveRoom_ShouldReturnBadRequest()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var semester = await SeedSemesterSchedulingScenarioAsync(context, addRoom: false);

            var response = await new ScheduleOptimizationService(context).AutoScheduleSemesterAsync(
                new AutoScheduleSemesterRequestDto
                {
                    SemesterId = semester.Id,
                    MinClassSize = 1,
                    Constraints = new AutoScheduleConstraintDto { SessionsPerWeek = 1 }
                });

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
            response.Message.Should().Be("ERR_NO_ACTIVE_ROOMS");
            context.Classes.Should().BeEmpty();
        }

        [Fact]
        public async Task AutoScheduleSemesterAsync_WithUnknownTimePreference_ShouldFallbackToAllSlots()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var semester = await SeedSemesterSchedulingScenarioAsync(context);

            var response = await new ScheduleOptimizationService(context).AutoScheduleSemesterAsync(
                new AutoScheduleSemesterRequestDto
                {
                    SemesterId = semester.Id,
                    MinClassSize = 1,
                    Constraints = new AutoScheduleConstraintDto
                    {
                        SessionsPerWeek = 1,
                        TimePreferences = new List<string> { "unknown" }
                    }
                });

            response.Success.Should().BeTrue();
            response.Message.Should().Be("AUTO_SCHEDULING_SEMESTER_COMPLETED");
            response.Data.Should().ContainSingle();
        }

        [Fact]
        public async Task AutoScheduleSemesterAsync_WhenSolverIsInfeasible_ShouldReturnConflict()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var semester = await SeedSemesterSchedulingScenarioAsync(context, roomCapacity: 0);

            var response = await new ScheduleOptimizationService(context).AutoScheduleSemesterAsync(
                new AutoScheduleSemesterRequestDto
                {
                    SemesterId = semester.Id,
                    MinClassSize = 1,
                    Constraints = new AutoScheduleConstraintDto { SessionsPerWeek = 1 }
                });

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
            response.Message.Should().NotBeNullOrWhiteSpace();
            context.Classes.Should().BeEmpty();
        }

        [Fact]
        public async Task AutoScheduleSemesterAsync_WithValidData_ShouldCreateClassAndScheduleRegistrations()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var semester = await SeedSemesterSchedulingScenarioAsync(context, studentCount: 2);

            var response = await new ScheduleOptimizationService(context).AutoScheduleSemesterAsync(
                new AutoScheduleSemesterRequestDto
                {
                    SemesterId = semester.Id,
                    MinClassSize = 1,
                    MaxClassSize = 15,
                    Constraints = new AutoScheduleConstraintDto { SessionsPerWeek = 1 }
                });

            response.Success.Should().BeTrue();
            response.StatusCode.Should().Be(StatusCodes.Status200OK);
            response.Message.Should().Be("AUTO_SCHEDULING_SEMESTER_COMPLETED");
            response.Data.Should().NotBeEmpty();
            context.Classes.Should().NotBeEmpty();
            context.StudentRegistrations
                .Should().OnlyContain(registration =>
                    registration.Status == (int)StudentRegistrationStatus.Scheduled);
        }

        [Fact]
        public async Task AutoScheduleSemesterAsync_WhenPersistenceFails_ShouldReturnInternalServerError()
        {
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int semesterId;
            using (var seedContext = new ApplicationDbContext(options, mockHttp.Object))
            {
                semesterId = (await SeedSemesterSchedulingScenarioAsync(seedContext)).Id;
            }

            var mockContext = new Mock<ApplicationDbContext>(options, mockHttp.Object)
            {
                CallBase = true
            };
            mockContext
                .Setup(context => context.SaveChangesAsync(It.IsAny<System.Threading.CancellationToken>()))
                .ThrowsAsync(new DbUpdateException("Database Exception"));

            var response = await new ScheduleOptimizationService(mockContext.Object).AutoScheduleSemesterAsync(
                new AutoScheduleSemesterRequestDto
                {
                    SemesterId = semesterId,
                    MinClassSize = 1,
                    Constraints = new AutoScheduleConstraintDto { SessionsPerWeek = 1 }
                });

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
            response.Message.Should().Contain("Error writing auto scheduling semester results");
            response.Message.Should().Contain("Database Exception");
            mockContext.Object.Dispose();
        }

        [Fact]
        public async Task SaveSemesterScheduleDraftAsync_ShouldAutoCreateInitialScheduleVersion()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var (semester, request) = await SeedDraftReadyScenarioAsync(context);

            var response = await new ScheduleOptimizationService(context).SaveSemesterScheduleDraftAsync(request);

            response.Success.Should().BeTrue();
            var versions = await context.ScheduleVersions.Where(v => v.SemesterId == semester.Id).ToListAsync();
            versions.Should().ContainSingle();
            versions[0].Name.Should().Be("Original");
            versions[0].ScheduleJson.Should().Contain(request.Classes[0].Code);
        }

        [Fact]
        public async Task SaveSemesterScheduleDraftAsync_ShouldPopulateTextSearchForCreatedClasses()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var (semester, request) = await SeedDraftReadyScenarioAsync(context);

            var response = await new ScheduleOptimizationService(context).SaveSemesterScheduleDraftAsync(request);

            response.Success.Should().BeTrue();
            var createdClass = await context.Classes.SingleAsync(c => c.SemesterId == semester.Id);
            createdClass.TextSearch.Should().NotBeNullOrWhiteSpace();
            createdClass.TextSearch.Should().Contain(createdClass.Code);
            createdClass.TextSearch.Should().Contain(createdClass.Name);
        }

        [Fact]
        public async Task SaveScheduleVersionAsync_WhenSemesterNotStarted_ShouldPersistSnapshotOfLiveState()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var (semester, request) = await SeedDraftReadyScenarioAsync(context);
            var service = new ScheduleOptimizationService(context);
            await service.SaveSemesterScheduleDraftAsync(request);

            var persistedClass = await context.Classes.SingleAsync(c => c.SemesterId == semester.Id);
            persistedClass.WeeklySchedulesJson = "[{\"dayOfWeek\":2,\"startTime\":\"09:00\",\"endTime\":\"11:00\"}]";
            await context.SaveChangesAsync();

            var response = await service.SaveScheduleVersionAsync(semester.Id, "Bản tôi thích");

            response.Success.Should().BeTrue();
            response.Data!.Name.Should().Be("Bản tôi thích");
            response.Data.ClassCount.Should().Be(1);
            var savedVersion = await context.ScheduleVersions
                .SingleAsync(v => v.SemesterId == semester.Id && v.Name == "Bản tôi thích");
            savedVersion.ScheduleJson.Should().Contain("\"StartTime\":\"09:00\"");
        }

        [Fact]
        public async Task SaveScheduleVersionAsync_WhenSemesterAlreadyStarted_ShouldStillSaveIfPlanningClassesExist()
        {
            // Scheduling is scoped by Class.Status, not by semester dates (a manually-created class
            // can start later than the semester itself), so an elapsed Semester.StartDate must not block this.
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var (semester, request) = await SeedDraftReadyScenarioAsync(context);
            var service = new ScheduleOptimizationService(context);
            await service.SaveSemesterScheduleDraftAsync(request);

            semester.StartDate = DateTime.UtcNow.Date.AddDays(-1);
            await context.SaveChangesAsync();

            var response = await service.SaveScheduleVersionAsync(semester.Id, "V1");

            response.Success.Should().BeTrue();
        }

        [Fact]
        public async Task SaveScheduleVersionAsync_WhenNameEmpty_ShouldReturnBadRequest()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var semester = await SeedSemesterSchedulingScenarioAsync(context);

            var response = await new ScheduleOptimizationService(context).SaveScheduleVersionAsync(semester.Id, "   ");

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
            response.Message.Should().Be("ERR_VERSION_NAME_REQUIRED");
        }

        [Fact]
        public async Task GetScheduleVersionsAsync_ShouldReturnVersionsNewestFirstWithClassCount()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var (semester, request) = await SeedDraftReadyScenarioAsync(context);
            var service = new ScheduleOptimizationService(context);
            await service.SaveSemesterScheduleDraftAsync(request);
            await service.SaveScheduleVersionAsync(semester.Id, "Version 2");

            var response = await service.GetScheduleVersionsAsync(semester.Id);

            response.Success.Should().BeTrue();
            response.Data.Should().HaveCount(2);
            response.Data.Should().OnlyContain(v => v.ClassCount == 1);
            response.Data!.Select(v => v.Name).Should().BeEquivalentTo("Original", "Version 2");
            response.Data.Should().BeInDescendingOrder(v => v.CreatedAt);
        }

        [Fact]
        public async Task DeleteScheduleVersionAsync_ShouldSoftDeleteVersion()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var (semester, request) = await SeedDraftReadyScenarioAsync(context);
            var service = new ScheduleOptimizationService(context);
            await service.SaveSemesterScheduleDraftAsync(request);
            var version = await context.ScheduleVersions.SingleAsync(v => v.SemesterId == semester.Id);

            var response = await service.DeleteScheduleVersionAsync(version.Id);

            response.Success.Should().BeTrue();
            var reloaded = await context.ScheduleVersions.IgnoreQueryFilters().SingleAsync(v => v.Id == version.Id);
            reloaded.IsDeleted.Should().BeTrue();

            var listResponse = await service.GetScheduleVersionsAsync(semester.Id);
            listResponse.Data.Should().BeEmpty();
        }

        [Fact]
        public async Task DeleteScheduleVersionAsync_WhenVersionNotFound_ShouldReturn404()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);

            var response = await new ScheduleOptimizationService(context).DeleteScheduleVersionAsync(9999);

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
            response.Message.Should().Be("ERR_SCHEDULE_VERSION_NOT_FOUND");
        }

        [Fact]
        public async Task RollbackSemesterScheduleAsync_ShouldRestoreClassesFromChosenVersion()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var (semester, request) = await SeedDraftReadyScenarioAsync(context);
            var originalCode = request.Classes[0].Code;
            var service = new ScheduleOptimizationService(context);

            await service.SaveSemesterScheduleDraftAsync(request);
            var v1 = await context.ScheduleVersions.SingleAsync(v => v.SemesterId == semester.Id);

            var persistedClass = await context.Classes.SingleAsync(c => c.SemesterId == semester.Id);
            persistedClass.WeeklySchedulesJson = "[{\"dayOfWeek\":3,\"startTime\":\"13:00\",\"endTime\":\"15:00\"}]";
            await context.SaveChangesAsync();
            await service.SaveScheduleVersionAsync(semester.Id, "Version 2");

            persistedClass.WeeklySchedulesJson = "[{\"dayOfWeek\":4,\"startTime\":\"16:00\",\"endTime\":\"18:00\"}]";
            await context.SaveChangesAsync();

            var response = await service.RollbackSemesterScheduleAsync(semester.Id, v1.Id);

            response.Success.Should().BeTrue();
            var restoredClass = await context.Classes.SingleAsync(c => c.SemesterId == semester.Id && c.Code == originalCode);
            restoredClass.WeeklySchedulesJson.Should().Contain("08:00");
            restoredClass.WeeklySchedulesJson.Should().NotContain("13:00");
            restoredClass.WeeklySchedulesJson.Should().NotContain("16:00");
        }

        [Fact]
        public async Task RollbackSemesterScheduleAsync_WhenSemesterAlreadyStarted_ShouldStillRollbackPlanningClasses()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var (semester, request) = await SeedDraftReadyScenarioAsync(context);
            var service = new ScheduleOptimizationService(context);
            await service.SaveSemesterScheduleDraftAsync(request);
            var version = await context.ScheduleVersions.SingleAsync(v => v.SemesterId == semester.Id);

            semester.StartDate = DateTime.UtcNow.Date.AddDays(-1);
            await context.SaveChangesAsync();

            var response = await service.RollbackSemesterScheduleAsync(semester.Id, version.Id);

            response.Success.Should().BeTrue();
            (await context.Classes.CountAsync(c => c.SemesterId == semester.Id && !c.IsDeleted)).Should().Be(1);
        }

        [Fact]
        public async Task RollbackSemesterScheduleAsync_WhenAClassHasProgressedPastPlanning_ShouldNotTouchOrDuplicateIt()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var (semester, request) = await SeedDraftReadyScenarioAsync(context);
            var service = new ScheduleOptimizationService(context);
            await service.SaveSemesterScheduleDraftAsync(request);
            var version = await context.ScheduleVersions.SingleAsync(v => v.SemesterId == semester.Id);

            // Simulate the class having actually started (e.g. its own StartDate arrived and the
            // lazy status job flipped it to Active) since the version was captured.
            var liveClass = await context.Classes.SingleAsync(c => c.SemesterId == semester.Id);
            liveClass.Status = (int)ClassStatus.Active;
            await context.SaveChangesAsync();

            var response = await service.RollbackSemesterScheduleAsync(semester.Id, version.Id);

            response.Success.Should().BeTrue();
            var classesForSemester = await context.Classes
                .Where(c => c.SemesterId == semester.Id && !c.IsDeleted)
                .ToListAsync();
            classesForSemester.Should().ContainSingle(); // no duplicate created alongside the Active class
            classesForSemester[0].Id.Should().Be(liveClass.Id);
            classesForSemester[0].Status.Should().Be((int)ClassStatus.Active);
        }

        [Fact]
        public async Task RollbackSemesterScheduleAsync_WhenVersionDoesNotExist_ShouldReturn404()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var semester = await SeedSemesterSchedulingScenarioAsync(context);

            var response = await new ScheduleOptimizationService(context).RollbackSemesterScheduleAsync(semester.Id, 9999);

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
            response.Message.Should().Be("ERR_SCHEDULE_VERSION_NOT_FOUND");
        }

        [Fact]
        public async Task RollbackSemesterScheduleAsync_WhenVersionBelongsToDifferentSemester_ShouldReturnBadRequest()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var (semesterA, requestA) = await SeedDraftReadyScenarioAsync(context);
            var service = new ScheduleOptimizationService(context);
            await service.SaveSemesterScheduleDraftAsync(requestA);
            var versionFromA = await context.ScheduleVersions.SingleAsync(v => v.SemesterId == semesterA.Id);

            var semesterB = await SeedSemesterSchedulingScenarioAsync(context);

            var response = await service.RollbackSemesterScheduleAsync(semesterB.Id, versionFromA.Id);

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
            response.Message.Should().Be("ERR_VERSION_SEMESTER_MISMATCH");
        }

        [Fact]
        public async Task RollbackSemesterScheduleAsync_WhenSnapshotHasNoClasses_ShouldReturnCorruptedBackupError()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var semester = await SeedSemesterSchedulingScenarioAsync(context);
            var emptyVersion = new ScheduleVersion
            {
                SemesterId = semester.Id,
                Name = "Empty",
                ScheduleJson = System.Text.Json.JsonSerializer.Serialize(new ScheduleVersionSnapshotDto { SemesterId = semester.Id })
            };
            context.ScheduleVersions.Add(emptyVersion);
            await context.SaveChangesAsync();

            var response = await new ScheduleOptimizationService(context).RollbackSemesterScheduleAsync(semester.Id, emptyVersion.Id);

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
            response.Message.Should().Be("ERR_CORRUPTED_BACKUP_DATA");
        }

        [Fact]
        public async Task SaveScheduleVersionAsync_WhenMaxVersionsReached_ShouldReturnBadRequest()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var (semester, request) = await SeedDraftReadyScenarioAsync(context);
            var service = new ScheduleOptimizationService(context);
            await service.SaveSemesterScheduleDraftAsync(request); // 1 version so far ("Original")

            for (var i = 0; i < 19; i++)
            {
                context.ScheduleVersions.Add(new ScheduleVersion
                {
                    SemesterId = semester.Id,
                    Name = $"Filler {i}",
                    ScheduleJson = "{\"semesterId\":0,\"classes\":[]}"
                });
            }
            await context.SaveChangesAsync(); // now 20 total

            var response = await service.SaveScheduleVersionAsync(semester.Id, "One too many");

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
            response.Message.Should().Be("ERR_MAX_SCHEDULE_VERSIONS_REACHED");
        }

        [Fact]
        public async Task DeleteScheduleVersionAsync_WhenVersionIsAutoSaved_ShouldReturnBadRequest()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var (semester, request) = await SeedDraftReadyScenarioAsync(context);
            var service = new ScheduleOptimizationService(context);
            await service.SaveSemesterScheduleDraftAsync(request);
            var autoVersion = await context.ScheduleVersions.SingleAsync(v => v.SemesterId == semester.Id);
            autoVersion.IsAutoSaved.Should().BeTrue();

            var response = await service.DeleteScheduleVersionAsync(autoVersion.Id);

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
            response.Message.Should().Be("ERR_CANNOT_DELETE_AUTO_VERSION");
            var reloaded = await context.ScheduleVersions.SingleAsync(v => v.Id == autoVersion.Id);
            reloaded.IsDeleted.Should().BeFalse();
        }

        [Fact]
        public async Task GetScheduleVersionPreviewAsync_ShouldReturnMaterializedSchedulesForVersion()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var (semester, request) = await SeedDraftReadyScenarioAsync(context);
            var service = new ScheduleOptimizationService(context);
            await service.SaveSemesterScheduleDraftAsync(request);
            var version = await context.ScheduleVersions.SingleAsync(v => v.SemesterId == semester.Id);

            var response = await service.GetScheduleVersionPreviewAsync(version.Id);

            response.Success.Should().BeTrue();
            response.Data.Should().ContainSingle();
            var previewClass = response.Data![0];
            previewClass.Id.Should().Be(0);
            previewClass.Code.Should().Be(request.Classes[0].Code);
            previewClass.TeacherName.Should().NotBeNullOrWhiteSpace();
            previewClass.Schedules.Should().NotBeEmpty();
            previewClass.Schedules.Should().OnlyContain(s => s.StartTime == "08:00");
        }

        [Fact]
        public async Task GetScheduleVersionPreviewAsync_WhenVersionNotFound_ShouldReturn404()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);

            var response = await new ScheduleOptimizationService(context).GetScheduleVersionPreviewAsync(9999);

            response.Success.Should().BeFalse();
            response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
            response.Message.Should().Be("ERR_SCHEDULE_VERSION_NOT_FOUND");
        }

        [Fact]
        public async Task SaveSemesterScheduleDraftAsync_WhenCalledTwice_ShouldOverwriteAutoVersionNotDuplicate()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var (semester, request) = await SeedDraftReadyScenarioAsync(context);
            var service = new ScheduleOptimizationService(context);
            await service.SaveSemesterScheduleDraftAsync(request);

            var secondRequest = new SaveScheduleDraftRequestDto
            {
                SemesterId = semester.Id,
                Classes = new List<ClassDraftSaveDto>
                {
                    new ClassDraftSaveDto
                    {
                        Code = $"CLS2-{Guid.NewGuid():N}",
                        Name = "Second Draft Class",
                        CourseId = request.Classes[0].CourseId,
                        TeacherId = request.Classes[0].TeacherId,
                        ExpectedLessons = 1,
                        WeeklySchedules = request.Classes[0].WeeklySchedules,
                        Students = new List<StudentEnrollDto>()
                    }
                }
            };
            await service.SaveSemesterScheduleDraftAsync(secondRequest);

            var versions = await context.ScheduleVersions
                .Where(v => v.SemesterId == semester.Id && v.IsAutoSaved)
                .ToListAsync();
            versions.Should().ContainSingle();
            versions[0].ScheduleJson.Should().Contain(secondRequest.Classes[0].Code);
        }

        [Fact]
        public async Task PurgeScheduleVersionsIfSemesterEmptyAsync_WhenNoPlanningClassesRemain_ShouldDeleteAllVersions()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var (semester, request) = await SeedDraftReadyScenarioAsync(context);
            var service = new ScheduleOptimizationService(context);
            await service.SaveSemesterScheduleDraftAsync(request);
            await service.SaveScheduleVersionAsync(semester.Id, "Extra version");
            (await context.ScheduleVersions.CountAsync(v => v.SemesterId == semester.Id)).Should().Be(2);

            var cls = await context.Classes.SingleAsync(c => c.SemesterId == semester.Id);
            cls.IsDeleted = true; // simulate ClassService.DeleteAsync's soft delete
            await context.SaveChangesAsync();

            await service.PurgeScheduleVersionsIfSemesterEmptyAsync(semester.Id);

            (await context.ScheduleVersions.CountAsync(v => v.SemesterId == semester.Id)).Should().Be(0);
        }

        [Fact]
        public async Task PurgeScheduleVersionsIfSemesterEmptyAsync_WhenPlanningClassesStillExist_ShouldKeepVersions()
        {
            var options = CreateNewContextOptions();
            using var context = new ApplicationDbContext(options, GetMockHttpContextAccessor().Object);
            var (semester, request) = await SeedDraftReadyScenarioAsync(context);
            var service = new ScheduleOptimizationService(context);
            await service.SaveSemesterScheduleDraftAsync(request);

            await service.PurgeScheduleVersionsIfSemesterEmptyAsync(semester.Id);

            (await context.ScheduleVersions.CountAsync(v => v.SemesterId == semester.Id)).Should().Be(1);
        }

        [Fact(Skip = "Intentional failure retained only as a demonstration test.")]
        public void Abnormal_IntentionalFailure_ShouldFail()
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
