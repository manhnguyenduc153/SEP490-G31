using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using FluentAssertions;
using sep490_be.DTO;
using sep490_be.DTO.Room;
using sep490_be.Enums;
using sep490_be.Models;
using sep490_be.Repositories.Common;
using sep490_be.Repositories.Implementations;
using sep490_be.Services.Implementations;
using Xunit;

namespace sep490_be.Tests.Services
{
    public class RoomServiceTests
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

        private static RoomService CreateService(ApplicationDbContext context)
        {
            var uow = new UnitOfWork<ApplicationDbContext>(context);
            var repo = new RoomRepository(context, uow);
            return new RoomService(repo);
        }

        #region Normal Test Cases

        [Fact]
        public async Task Normal_GetAllAsync_ShouldReturnRooms()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                context.Rooms.AddRange(
                    new Room { Code = "R101", Name = "Room 101", Status = (int)RoomStatus.Active, RoomType = RoomType.Theory, TextSearch = "R101 Room 101" },
                    new Room { Code = "R102", Name = "Room 102", Status = (int)RoomStatus.Inactive, RoomType = RoomType.Pratice, TextSearch = "R102 Room 102" }
                );
                await context.SaveChangesAsync();
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateService(context);
                
                var responseAll = await service.GetAllAsync(new RoomSearchDto { PageIndex = 1, PageSize = 10 });
                var responseActive = await service.GetAllAsync(new RoomSearchDto { Status = true, PageIndex = 1, PageSize = 10 });

                // Assert
                responseAll.Success.Should().BeTrue();
                responseAll.Data!.Items.Should().HaveCount(2);

                responseActive.Success.Should().BeTrue();
                responseActive.Data!.Items.Should().ContainSingle();
                responseActive.Data!.Items.First().Code.Should().Be("R101");
            }
        }

        [Fact]
        public async Task Normal_CreateAsync_WithValidInputs_ShouldCreateRoom()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            var dto = new RoomSaveDto
            {
                Code = "NEW_R",
                Name = "New Room",
                Capacity = 30,
                Status = (int)RoomStatus.Active,
                RoomType = RoomType.Theory,
                Building = "A",
                Floor = "1",
                Image = "url_image"
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateService(context);
                var response = await service.CreateAsync(dto);

                // Assert
                response.Success.Should().BeTrue();
                response.StatusCode.Should().Be(StatusCodes.Status201Created);
                response.Data!.Code.Should().Be("NEW_R");

                var exists = await context.Rooms.AnyAsync(r => r.Code == "NEW_R");
                exists.Should().BeTrue();
            }
        }

        [Fact]
        public async Task Normal_EditAsync_WithValidInputs_ShouldUpdateRoom()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int roomId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var room = new Room { Code = "R1", Name = "Room 1", Capacity = 20, RoomType = RoomType.Theory, Building = "A", Floor = "1", Image = "i" };
                context.Rooms.Add(room);
                await context.SaveChangesAsync();
                roomId = room.Id;
            }

            var dto = new RoomSaveDto
            {
                Id = roomId,
                Code = "R1_UPD",
                Name = "Room 1 Updated",
                Capacity = 40,
                Status = (int)RoomStatus.Active,
                RoomType = RoomType.Theory,
                Building = "A",
                Floor = "2",
                Image = "img"
            };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateService(context);
                var response = await service.EditAsync(dto);

                // Assert
                response.Success.Should().BeTrue();
                response.Data!.Code.Should().Be("R1_UPD");
                response.Data!.Capacity.Should().Be(40);

                var updated = await context.Rooms.FindAsync(roomId);
                updated!.Code.Should().Be("R1_UPD");
            }
        }

        [Fact]
        public async Task Normal_GetStatsAsync_ShouldReturnCorrectStats()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                context.Rooms.AddRange(
                    new Room { Code = "R1", Name = "Room 1", Status = (int)RoomStatus.Active },
                    new Room { Code = "R2", Name = "Room 2", Status = (int)RoomStatus.Inactive },
                    new Room { Code = "R3", Name = "Room 3", Status = (int)RoomStatus.Maintaince }
                );
                await context.SaveChangesAsync();
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateService(context);
                var response = await service.GetStatsAsync();

                // Assert
                response.Success.Should().BeTrue();
                response.Data!.TotalRooms.Should().Be(3);
                response.Data.AvailableRooms.Should().Be(1);
                response.Data.MaintenanceRooms.Should().Be(1);
            }
        }

        [Fact]
        public async Task Normal_DeleteAsync_WhenRoomExists_ShouldSoftDeleteRoom()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();
            int roomId;

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var room = new Room { Code = "R1", Name = "Room 1" };
                context.Rooms.Add(room);
                await context.SaveChangesAsync();
                roomId = room.Id;
            }

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateService(context);
                var response = await service.DeleteAsync(roomId);

                // Assert
                response.Success.Should().BeTrue();

                var deletedRoom = await context.Rooms.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == roomId);
                Assert.NotNull(deletedRoom);
                deletedRoom!.IsDeleted.Should().BeTrue();
            }
        }

        #endregion

        #region Abnormal Test Cases

        [Fact]
        public async Task Abnormal_CreateAsync_WithDuplicateName_ShouldReturnBadRequest()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                context.Rooms.Add(new Room { Code = "R1", Name = "DUP_NAME" });
                await context.SaveChangesAsync();
            }

            var dto = new RoomSaveDto { Code = "R2", Name = "DUP_NAME", Capacity = 10, Building = "A", Floor = "1", Image = "i" };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateService(context);
                var response = await service.CreateAsync(dto);

                // Assert
                response.Success.Should().BeFalse();
                response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
                response.Message.Should().Be("ERR_ROOM_NAME_DUPLICATE");
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
                context.Rooms.Add(new Room { Code = "DUP_CODE", Name = "R1" });
                await context.SaveChangesAsync();
            }

            var dto = new RoomSaveDto { Code = "DUP_CODE", Name = "R2", Capacity = 10, Building = "A", Floor = "1", Image = "i" };

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateService(context);
                var response = await service.CreateAsync(dto);

                // Assert
                response.Success.Should().BeFalse();
                response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
                response.Message.Should().Be("ERR_ROOM_CODE_DUPLICATE");
            }
        }

        [Fact]
        public async Task Abnormal_GetByIdAsync_WhenNotFound_ShouldReturnNotFound()
        {
            // Arrange
            var options = CreateNewContextOptions();
            var mockHttp = GetMockHttpContextAccessor();

            // Act
            using (var context = new ApplicationDbContext(options, mockHttp.Object))
            {
                var service = CreateService(context);
                var response = await service.GetByIdAsync(999);

                // Assert
                response.Success.Should().BeFalse();
                response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
            }
        }

        #endregion
    }
}
