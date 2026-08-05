using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using FluentAssertions;
using Moq;
using Xunit;
using sep490_be.DTO;
using sep490_be.DTO.Student;
using sep490_be.Models;
using sep490_be.Enums;
using sep490_be.Repositories.Implementations;
using sep490_be.Repositories.Common;
using sep490_be.Services.Implementations;

namespace sep490_be.Tests.Services
{
    /// <summary>
    /// Unit test suite for StudentService.
    /// Code Module: StudentService
    /// </summary>
    public class StudentServiceTests
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

        private (UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager) CreateIdentityManagers(ApplicationDbContext context)
        {
            var userStore = new UserStore<IdentityUser>(context);
            
            var optionsAccessorMock = new Mock<Microsoft.Extensions.Options.IOptions<IdentityOptions>>();
            optionsAccessorMock.Setup(o => o.Value).Returns(new IdentityOptions());
            
            var servicesMock = new Mock<IServiceProvider>();

            var userManager = new UserManager<IdentityUser>(
                userStore,
                optionsAccessor: optionsAccessorMock.Object,
                passwordHasher: new PasswordHasher<IdentityUser>(),
                userValidators: new List<IUserValidator<IdentityUser>> { new UserValidator<IdentityUser>() },
                passwordValidators: new List<IPasswordValidator<IdentityUser>>(), // Empty list bypasses password complexity validation
                keyNormalizer: new UpperInvariantLookupNormalizer(),
                errors: new IdentityErrorDescriber(),
                services: servicesMock.Object,
                logger: new NullLogger<UserManager<IdentityUser>>());

            var roleStore = new RoleStore<IdentityRole>(context);
            var roleManager = new RoleManager<IdentityRole>(
                roleStore,
                roleValidators: new List<IRoleValidator<IdentityRole>> { new RoleValidator<IdentityRole>() },
                keyNormalizer: new UpperInvariantLookupNormalizer(),
                errors: new IdentityErrorDescriber(),
                logger: new NullLogger<RoleManager<IdentityRole>>());

            return (userManager, roleManager);
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
                var s1 = new Student { Code = "ST001", Name = "Nguyen Van A", Email = "a@test.com", Phone = "111", TextSearch = "ST001 Nguyen Van A a@test.com 111", Status = (int)StudentStatus.Active };
                var s2 = new Student { Code = "ST002", Name = "Tran Van B", Email = "b@test.com", Phone = "222", TextSearch = "ST002 Tran Van B b@test.com 222", Status = (int)StudentStatus.Active };
                context.Students.AddRange(s1, s2);
                await context.SaveChangesAsync();
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var repo = new StudentRepository(context, uow);
                var service = new StudentService(repo, userManager, roleManager);

                var searchDto = new StudentSearchDto
                {
                    Keyword = "Nguyen",
                    PageIndex = 1,
                    PageSize = 10
                };

                var response = await service.GetAllAsync(searchDto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data.Should().NotBeNull();
                response.Data!.Items.Should().HaveCount(1);
                response.Data!.Items.First().Name.Should().Be("Nguyen Van A");
            }
        }

        [Fact]
        public async Task Normal_GetAllAsync_WithFilters_ShouldReturnFilteredResults()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var s1 = new Student { Code = "ST001", Name = "Nguyen Van A", Gender = true, GradeLevel = 10, Status = (int)StudentStatus.Active };
                var s2 = new Student { Code = "ST002", Name = "Tran Van B", Gender = false, GradeLevel = 11, Status = (int)StudentStatus.Inactive };
                context.Students.AddRange(s1, s2);
                await context.SaveChangesAsync();
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var repo = new StudentRepository(context, uow);
                var service = new StudentService(repo, userManager, roleManager);

                var searchDto = new StudentSearchDto
                {
                    StudentStatus = (int)StudentStatus.Active,
                    GradeLevel = 10,
                    Gender = true,
                    PageIndex = 1,
                    PageSize = 10
                };

                var response = await service.GetAllAsync(searchDto);

                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data!.Items.Should().HaveCount(1);
                response.Data!.Items.First().Code.Should().Be("ST001");
            }
        }

        [Fact]
        public async Task Normal_GetByIdAsync_WhenStudentExists_ShouldReturnStudent()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int createdId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var student = new Student { Code = "ST001", Name = "Nguyen Van A", Email = "student1@test.com", Status = (int)StudentStatus.Active };
                context.Students.Add(student);
                await context.SaveChangesAsync();
                createdId = student.Id;
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var repo = new StudentRepository(context, uow);
                var service = new StudentService(repo, userManager, roleManager);

                var response = await service.GetByIdAsync(createdId);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data.Should().NotBeNull();
                response.Data!.Code.Should().Be("ST001");
                response.Data.HasAccount.Should().BeFalse();
            }
        }

        [Fact]
        public async Task Normal_CreateAsync_WithValidDto_ShouldCreateStudentAndIdentityAccount()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            var dto = new StudentSaveDto
            {
                Code = "ST001",
                Name = "Nguyen Van A",
                Email = "student1@test.com",
                Phone = "0987654321",
                Status = (int)StudentStatus.Active
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var repo = new StudentRepository(context, uow);
                var service = new StudentService(repo, userManager, roleManager);

                var response = await service.CreateAsync(dto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data.Should().NotBeNull();
                response.Data!.Code.Should().Be("ST001");
                response.Data.HasAccount.Should().BeTrue();

                // Verify user account exists in Identity DB
                var createdUser = await userManager.FindByEmailAsync("student1@test.com");
                createdUser.Should().NotBeNull();
                createdUser!.Email.Should().Be("student1@test.com");
                
                var roles = await userManager.GetRolesAsync(createdUser!);
                roles.Should().Contain("Student");
            }
        }

        [Fact]
        public async Task Normal_EditAsync_WithValidDto_ShouldModifyStudent()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int studentId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var student = new Student { Code = "ST001", Name = "Nguyen Van A", Email = "student1@test.com", Status = (int)StudentStatus.Active };
                context.Students.Add(student);
                await context.SaveChangesAsync();
                studentId = student.Id;
            }

            var dto = new StudentSaveDto
            {
                Id = studentId,
                Code = "ST001",
                Name = "Nguyen Van A Updated",
                Email = "student1@test.com",
                Status = (int)StudentStatus.Active,
                SchoolName = "FPT School"
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var repo = new StudentRepository(context, uow);
                var service = new StudentService(repo, userManager, roleManager);

                var response = await service.EditAsync(dto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data.Should().NotBeNull();
                response.Data!.Name.Should().Be("Nguyen Van A Updated");
                response.Data.SchoolName.Should().Be("FPT School");
            }
        }

        [Fact]
        public async Task Normal_EditAsync_WithEmailChange_ShouldSyncToIdentity()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int studentId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                var student = new Student { Code = "ST001", Name = "Nguyen Van A", Email = "oldemail@test.com", Status = (int)StudentStatus.Active };
                context.Students.Add(student);
                await context.SaveChangesAsync();
                studentId = student.Id;

                // Pre-create user with old email
                var user = new IdentityUser { UserName = "oldemail", Email = "oldemail@test.com", EmailConfirmed = true };
                await userManager.CreateAsync(user, "123456");
            }

            var dto = new StudentSaveDto
            {
                Id = studentId,
                Code = "ST001",
                Name = "Nguyen Van A",
                Email = "newemail@test.com",
                Status = (int)StudentStatus.Active
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var repo = new StudentRepository(context, uow);
                var service = new StudentService(repo, userManager, roleManager);

                var response = await service.EditAsync(dto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();

                // Identity User with old email should no longer exist under that email
                var oldUser = await userManager.FindByEmailAsync("oldemail@test.com");
                oldUser.Should().BeNull();

                // Identity User with new email should exist
                var newUser = await userManager.FindByEmailAsync("newemail@test.com");
                newUser.Should().NotBeNull();
                newUser!.UserName.Should().Be("newemail");
            }
        }

        [Fact]
        public async Task Normal_EditAsync_WhenActive_ShouldUnlockIdentityUser()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int studentId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                var student = new Student { Code = "ST001", Name = "Nguyen Van A", Email = "lockout@test.com", Status = (int)StudentStatus.Active };
                context.Students.Add(student);
                await context.SaveChangesAsync();
                studentId = student.Id;

                // Pre-create user and lock them out
                var user = new IdentityUser { UserName = "lockout", Email = "lockout@test.com", EmailConfirmed = true };
                await userManager.CreateAsync(user, "123456");
                await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
            }

            var dto = new StudentSaveDto
            {
                Id = studentId,
                Code = "ST001",
                Name = "Nguyen Van A",
                Email = "lockout@test.com",
                Status = (int)StudentStatus.Active
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var repo = new StudentRepository(context, uow);
                var service = new StudentService(repo, userManager, roleManager);

                var response = await service.EditAsync(dto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();

                var user = await userManager.FindByEmailAsync("lockout@test.com");
                var isLockedOut = await userManager.IsLockedOutAsync(user!);
                isLockedOut.Should().BeFalse();
            }
        }

        // DeleteAsync now performs a real hard delete via ExecuteDeleteAsync (bypassing the global
        // soft-delete interceptor on purpose — see IStudentRepository.HardDeleteAsync). The EF Core
        // InMemory provider used by this test suite does not support ExecuteDelete/ExecuteDeleteAsync
        // ("not supported by the current database provider"), so the success path can't be exercised
        // here; verify it against a relational provider (SQL Server/SQLite) or via manual testing.
        [Fact(Skip = "DeleteAsync uses ExecuteDeleteAsync for a real hard delete, which the EF Core InMemory provider does not support.")]
        public async Task Normal_DeleteAsync_WhenStudentExists_ShouldDeleteStudentAndIdentityAccount()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int studentId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                var student = new Student { Code = "ST001", Name = "Nguyen Van A", Email = "delete@test.com", Status = (int)StudentStatus.Active };
                context.Students.Add(student);
                await context.SaveChangesAsync();
                studentId = student.Id;

                var user = new IdentityUser { UserName = "delete", Email = "delete@test.com", EmailConfirmed = true };
                await userManager.CreateAsync(user, "123456");
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var repo = new StudentRepository(context, uow);
                var service = new StudentService(repo, userManager, roleManager);

                var response = await service.DeleteAsync(studentId);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data.Should().BeTrue();

                var dbStudent = await context.Students.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == studentId);
                dbStudent.Should().BeNull(); // verify hard-deleted (row fully removed, not IsDeleted = true)

                var user = await userManager.FindByEmailAsync("delete@test.com");
                user.Should().BeNull();
            }
        }

        [Fact]
        public async Task Abnormal_DeleteAsync_WhenStudentInUse_ShouldReturnFail()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int studentId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var student = new Student { Code = "ST002", Name = "Nguyen Van B", Email = "inuse@test.com", Status = (int)StudentStatus.Active };
                var cls = new Class { Code = "CL01", Name = "Class A", Status = 1 };
                context.Students.Add(student);
                context.Classes.Add(cls);
                await context.SaveChangesAsync();
                studentId = student.Id;

                context.StudentClasses.Add(new StudentClass { StudentId = studentId, ClassId = cls.Id, Status = 1, EnrollDate = DateTime.UtcNow });
                await context.SaveChangesAsync();
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var repo = new StudentRepository(context, uow);
                var service = new StudentService(repo, userManager, roleManager);

                var response = await service.DeleteAsync(studentId);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.Message.Should().Be("ERR_STUDENT_IN_USE");

                var dbStudent = await context.Students.FirstOrDefaultAsync(s => s.Id == studentId);
                dbStudent.Should().NotBeNull(); // student must still exist
            }
        }

        [Fact]
        public async Task Normal_DeactiveAsync_WhenStudentExists_ShouldDeactivateAndLockoutIdentityUser()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int studentId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                var student = new Student { Code = "ST001", Name = "Nguyen Van A", Email = "deactive@test.com", Status = (int)StudentStatus.Active };
                context.Students.Add(student);
                await context.SaveChangesAsync();
                studentId = student.Id;

                var user = new IdentityUser { UserName = "deactive", Email = "deactive@test.com", EmailConfirmed = true };
                await userManager.CreateAsync(user, "123456");
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var repo = new StudentRepository(context, uow);
                var service = new StudentService(repo, userManager, roleManager);

                var response = await service.DeactiveAsync(studentId);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data.Should().BeTrue();

                // Student status in DB should update (soft delete checks or deactive logic)
                var dbStudent = await context.Students.FindAsync(studentId);
                dbStudent.Should().NotBeNull();
                dbStudent!.IsDeleted.Should().BeTrue();

                var user = await userManager.FindByEmailAsync("deactive@test.com");
                var isLockedOut = await userManager.IsLockedOutAsync(user!);
                isLockedOut.Should().BeTrue();
            }
        }

        [Fact]
        public async Task Normal_CheckEmailsAsync_ShouldReturnEmailMap()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var s1 = new Student { Code = "ST001", Name = "A", Email = "a@test.com", Status = (int)StudentStatus.Active };
                var s2 = new Student { Code = "ST002", Name = "B", Email = "b@test.com", Status = (int)StudentStatus.Active };
                context.Students.AddRange(s1, s2);
                await context.SaveChangesAsync();
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var repo = new StudentRepository(context, uow);
                var service = new StudentService(repo, userManager, roleManager);

                var response = await service.CheckEmailsAsync(new List<string> { "a@test.com", "B@TEST.COM", "c@test.com" });

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data.Should().ContainKey("a@test.com");
                response.Data.Should().ContainKey("b@test.com");
                response.Data.Should().NotContainKey("c@test.com");
            }
        }

        [Fact]
        public async Task Normal_ImportAsync_WithValidList_ShouldImportAll()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            var dtos = new List<StudentSaveDto>
            {
                new StudentSaveDto { Code = "ST001", Name = "Student A", Email = "a@test.com", Status = (int)StudentStatus.Active },
                new StudentSaveDto { Code = "", Name = "Student B", Email = "b@test.com", Status = (int)StudentStatus.Active } // Code empty will auto generate
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var repo = new StudentRepository(context, uow);
                var service = new StudentService(repo, userManager, roleManager);

                var response = await service.ImportAsync(dtos);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data.Should().HaveCount(2);
                response.Data[0].Code.Should().Be("ST001");
                response.Data[1].Code.Should().StartWith("ST_");
            }
        }

        [Fact]
        public async Task Normal_BulkProvisionAccountsAsync_ShouldCreateAccountsForSelectedStudents()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int id1, id2;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var s1 = new Student { Code = "ST001", Name = "Student 1", Email = "s1@test.com", Status = (int)StudentStatus.Active };
                var s2 = new Student { Code = "ST002", Name = "Student 2", Email = "s2@test.com", Status = (int)StudentStatus.Active };
                context.Students.AddRange(s1, s2);
                await context.SaveChangesAsync();
                id1 = s1.Id;
                id2 = s2.Id;
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var repo = new StudentRepository(context, uow);
                var service = new StudentService(repo, userManager, roleManager);

                var response = await service.BulkProvisionAccountsAsync(new List<int> { id1, id2 });

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data.Should().BeTrue();

                var u1 = await userManager.FindByEmailAsync("s1@test.com");
                u1.Should().NotBeNull();

                var u2 = await userManager.FindByEmailAsync("s2@test.com");
                u2.Should().NotBeNull();
            }
        }

        #endregion

        #region Boundary Test Cases (Kiểm thử giá trị biên)

        [Fact]
        public async Task Boundary_CreateAsync_WithCodeExactly50Characters_ShouldReturnCreated()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            var boundaryCode = new string('A', 50);

            var dto = new StudentSaveDto
            {
                Code = boundaryCode,
                Name = "Boundary Student",
                Email = "boundary@test.com",
                Status = (int)StudentStatus.Active
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var repo = new StudentRepository(context, uow);
                var service = new StudentService(repo, userManager, roleManager);

                var response = await service.CreateAsync(dto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data!.Code.Should().HaveLength(50);
            }
        }

        [Fact]
        public async Task Boundary_CreateAsync_WithNameExactly200Characters_ShouldReturnCreated()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            var boundaryName = new string('B', 200);

            var dto = new StudentSaveDto
            {
                Code = "STBOUNDARY",
                Name = boundaryName,
                Email = "boundary@test.com",
                Status = (int)StudentStatus.Active
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var repo = new StudentRepository(context, uow);
                var service = new StudentService(repo, userManager, roleManager);

                var response = await service.CreateAsync(dto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data!.Name.Should().HaveLength(200);
            }
        }

        [Fact]
        public async Task Boundary_CreateAsync_WithEmailExactly150Characters_ShouldReturnCreated()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            
            // Email parts: username + @domain.com. Let's make it exactly 150 chars.
            // 150 - 11 ("@domain.com") = 139 chars.
            var prefix = new string('c', 139);
            var boundaryEmail = $"{prefix}@domain.com";

            var dto = new StudentSaveDto
            {
                Code = "STBOUNDARY2",
                Name = "Boundary Email",
                Email = boundaryEmail,
                Status = (int)StudentStatus.Active
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var repo = new StudentRepository(context, uow);
                var service = new StudentService(repo, userManager, roleManager);

                var response = await service.CreateAsync(dto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeTrue();
                response.Data!.Email.Should().HaveLength(150);
            }
        }

        #endregion

        #region Abnormal Test Cases (Kiểm thử giá trị bất thường / Lỗi)

        [Fact]
        public async Task Abnormal_GetByIdAsync_WhenNotFound_ShouldReturnNotFound()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var repo = new StudentRepository(context, uow);
                var service = new StudentService(repo, userManager, roleManager);

                var response = await service.GetByIdAsync(9999);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
                response.Message.Should().Be("ERR_STUDENT_NOT_FOUND");
            }
        }

        [Fact]
        public async Task Abnormal_CreateAsync_WithEmptyCode_ShouldReturnBadRequest()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            var dto = new StudentSaveDto
            {
                Code = "   ",
                Name = "Test",
                Email = "test@test.com"
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var repo = new StudentRepository(context, uow);
                var service = new StudentService(repo, userManager, roleManager);

                var response = await service.CreateAsync(dto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
                response.Message.Should().Be("ERR_CODE_EMPTY");
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
                var student = new Student { Code = "DUPCODE", Name = "Student 1", Email = "s1@test.com", Status = (int)StudentStatus.Active };
                context.Students.Add(student);
                await context.SaveChangesAsync();
            }

            var dto = new StudentSaveDto
            {
                Code = "DUPCODE",
                Name = "Student 2",
                Email = "s2@test.com"
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var repo = new StudentRepository(context, uow);
                var service = new StudentService(repo, userManager, roleManager);

                var response = await service.CreateAsync(dto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
                response.Message.Should().Be("ERR_CODE_DUPLICATE");
            }
        }

        [Fact]
        public async Task Abnormal_CreateAsync_WithDuplicateEmail_ShouldReturnBadRequest()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var student = new Student { Code = "ST001", Name = "Student 1", Email = "dup@test.com", Status = (int)StudentStatus.Active };
                context.Students.Add(student);
                await context.SaveChangesAsync();
            }

            var dto = new StudentSaveDto
            {
                Code = "ST002",
                Name = "Student 2",
                Email = "dup@test.com"
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var repo = new StudentRepository(context, uow);
                var service = new StudentService(repo, userManager, roleManager);

                var response = await service.CreateAsync(dto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
                response.Message.Should().Be("ERR_EMAIL_DUPLICATE");
            }
        }

        [Fact]
        public async Task Abnormal_CreateAsync_WithDuplicateEmailInIdentity_ShouldReturnBadRequest()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                var user = new IdentityUser { UserName = "dup", Email = "dup@test.com", EmailConfirmed = true };
                await userManager.CreateAsync(user, "123456");
            }

            var dto = new StudentSaveDto
            {
                Code = "ST001",
                Name = "Student 1",
                Email = "dup@test.com"
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var repo = new StudentRepository(context, uow);
                var service = new StudentService(repo, userManager, roleManager);

                var response = await service.CreateAsync(dto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
                response.Message.Should().Be("ERR_EMAIL_DUPLICATE");
            }
        }

        [Fact]
        public async Task Abnormal_EditAsync_WhenNotFound_ShouldReturnNotFound()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            var dto = new StudentSaveDto
            {
                Id = 9999,
                Code = "ST001",
                Name = "Test",
                Email = "test@test.com"
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var repo = new StudentRepository(context, uow);
                var service = new StudentService(repo, userManager, roleManager);

                var response = await service.EditAsync(dto);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
                response.Message.Should().Be("ERR_STUDENT_NOT_FOUND");
            }
        }

        [Fact]
        public async Task Abnormal_DeleteAsync_WhenNotFound_ShouldReturnNotFound()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var repo = new StudentRepository(context, uow);
                var service = new StudentService(repo, userManager, roleManager);

                var response = await service.DeleteAsync(9999);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
                response.Message.Should().Be("ERR_STUDENT_NOT_FOUND");
            }
        }

        [Fact]
        public async Task Abnormal_DeactiveAsync_WhenNotFound_ShouldReturnNotFound()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var repo = new StudentRepository(context, uow);
                var service = new StudentService(repo, userManager, roleManager);

                var response = await service.DeactiveAsync(9999);

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
                response.Message.Should().Be("ERR_STUDENT_NOT_FOUND");
            }
        }

        [Fact]
        public async Task Abnormal_BulkProvisionAccountsAsync_WhenNullOrEmptyList_ShouldReturnBadRequest()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var repo = new StudentRepository(context, uow);
                var service = new StudentService(repo, userManager, roleManager);

                var response = await service.BulkProvisionAccountsAsync(new List<int>());

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
                response.Message.Should().Be("ERR_NO_STUDENTS_SELECTED");
            }
        }

        [Fact]
        public async Task Abnormal_BulkProvisionAccountsAsync_WhenNoneFound_ShouldReturnNotFound()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var (userManager, roleManager) = CreateIdentityManagers(context);
                var uow = new UnitOfWork<ApplicationDbContext>(context);
                var repo = new StudentRepository(context, uow);
                var service = new StudentService(repo, userManager, roleManager);

                var response = await service.BulkProvisionAccountsAsync(new List<int> { 9991, 9992 });

                // Assert
                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
                response.Message.Should().Be("ERR_STUDENTS_NOT_FOUND");
            }
        }

        #endregion
    }
}
