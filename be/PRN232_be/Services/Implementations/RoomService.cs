using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PRN232_be.DTO;
using PRN232_be.DTO.Room;
using PRN232_be.Models;
using PRN232_be.Repositories.Interfaces;
using PRN232_be.Services.Interfaces;
using PRN232_be.Helpers;

namespace PRN232_be.Services.Implementations
{
    public class RoomService : IRoomService
    {
        private readonly IRoomRepository _repository;

        public RoomService(IRoomRepository repository)
        {
            _repository = repository;
        }

        public async Task<ApiResponse<PagingResponse<RoomDto>>> GetAllAsync(RoomSearchDto searchDto)
        {
            try
            {
                var query = _repository.FindAll();

                if (!string.IsNullOrWhiteSpace(searchDto.Keyword))
                {
                    query = query.Where(c => c.TextSearch != null && c.TextSearch.Contains(searchDto.Keyword));
                }

                var totalRecords = await query.CountAsync();
                List<Room> entities;
                if (searchDto.PageSize <= 0)
                {
                    entities = await query.ToListAsync();
                }
                else
                {
                    entities = await query.ApplyPagingAsync(searchDto);
                }

                var dtos = entities.Select(MapToDto);
                var pagingResponse = dtos.ToPagingResponse(totalRecords, searchDto);

                return ApiResponse<PagingResponse<RoomDto>>.Ok(pagingResponse, "GET_ROOM_LIST_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<PagingResponse<RoomDto>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<RoomDto>> GetByIdAsync(int id)
        {
            try
            {
                var entity = await _repository.GetByIdAsync(id);
                if (entity == null)
                {
                    return ApiResponse<RoomDto>.Fail("ERR_ROOM_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                return ApiResponse<RoomDto>.Ok(MapToDto(entity), "GET_ROOM_DETAIL_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<RoomDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        private static RoomDto MapToDto(Room entity) => new()
        {
            Id = entity.Id,
            Code = entity.Code ?? string.Empty,
            Name = entity.Name ?? string.Empty,
            Capacity = entity.Capacity,
            Status = entity.Status
        };
    }
}
