using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using sep490_be.DTO;
using sep490_be.DTO.Room;
using sep490_be.Enums;
using sep490_be.Helpers;
using sep490_be.Models;
using sep490_be.Repositories.Interfaces;
using sep490_be.Repositories.Common;
using sep490_be.Services.Interfaces;

namespace sep490_be.Services.Implementations
{
    public class RoomService : IRoomService
    {
        private readonly IRoomRepository _repository;
        private readonly IBaseRepository<ClassSchedule, ApplicationDbContext> _scheduleRepository;
        private readonly IClassRepository _classRepository;

        public RoomService(
            IRoomRepository repository,
            IBaseRepository<ClassSchedule, ApplicationDbContext> scheduleRepository,
            IClassRepository classRepository)
        {
            _repository = repository;
            _scheduleRepository = scheduleRepository;
            _classRepository = classRepository;
        }

        // ──────────────────────────────────────────────────────────────
        // GET ALL
        // ──────────────────────────────────────────────────────────────
        public async Task<ApiResponse<PagingResponse<RoomDto>>> GetAllAsync(RoomSearchDto searchDto)
        {
            try
            {
                var query = _repository.FindAll();

                // Tìm kiếm theo keyword (Name, Code, Building, Floor)
                if (!string.IsNullOrWhiteSpace(searchDto.Keyword))
                    query = query.Where(r => r.TextSearch != null && r.TextSearch.Contains(searchDto.Keyword));



                // Lọc theo tòa nhà
                if (!string.IsNullOrWhiteSpace(searchDto.Building))
                    query = query.Where(r => r.Building == searchDto.Building);

                // Lọc theo sức chứa tối thiểu
                if (searchDto.MinCapacity.HasValue)
                    query = query.Where(r => r.Capacity >= searchDto.MinCapacity.Value);

                // Lọc theo trạng thái (Status từ BaseSearchDto: true = Active=1, false = Inactive=2)
                if (searchDto.Status.HasValue)
                {
                    var statusVal = searchDto.Status.Value ? (int)RoomStatus.Active : (int)RoomStatus.Inactive;
                    query = query.Where(r => r.Status == statusVal);
                }

                var totalRecords = await query.CountAsync();
                var entities = await query
                    .OrderBy(r => r.Name)
                    .ApplyPagingAsync(searchDto);

                var dtos = entities.Select(MapToDto);
                var pagingResponse = dtos.ToPagingResponse(totalRecords, searchDto);

                return ApiResponse<PagingResponse<RoomDto>>.Ok(pagingResponse, "GET_ROOM_LIST_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<PagingResponse<RoomDto>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        // ──────────────────────────────────────────────────────────────
        // GET BY ID
        // ──────────────────────────────────────────────────────────────
        public async Task<ApiResponse<RoomDto>> GetByIdAsync(int id)
        {
            try
            {
                var entity = await _repository.GetByIdAsync(id);
                if (entity == null)
                    return ApiResponse<RoomDto>.Fail("ERR_ROOM_NOT_FOUND", StatusCodes.Status404NotFound);

                return ApiResponse<RoomDto>.Ok(MapToDto(entity), "GET_ROOM_DETAIL_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<RoomDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        // ──────────────────────────────────────────────────────────────
        // CREATE — BR-11: Tên phòng phải là duy nhất
        // ──────────────────────────────────────────────────────────────
        public async Task<ApiResponse<RoomDto>> CreateAsync(RoomSaveDto dto)
        {
            try
            {
                var error = await ValidateAsync(dto, isEdit: false);
                if (error != null)
                    return ApiResponse<RoomDto>.Fail(error, StatusCodes.Status400BadRequest);

                var entity = new Room
                {
                    Id       = 0,
                    Code     = dto.Code.Trim(),
                    Name     = dto.Name.Trim(),
                    Capacity = dto.Capacity,
                    Status   = dto.Status != 0 ? dto.Status : (int)RoomStatus.Active,
                    Building = dto.Building?.Trim(),
                    Floor    = dto.Floor?.Trim(),
                    TextSearch = dto.TextSearch
                };

                await _repository.AddAsync(entity);
                await _repository.SaveChangesAsync();

                return ApiResponse<RoomDto>.Created(MapToDto(entity), "CREATE_ROOM_SUCCESS");
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                return ApiResponse<RoomDto>.Fail(msg, StatusCodes.Status500InternalServerError);
            }
        }

        // ──────────────────────────────────────────────────────────────
        // EDIT — BR-11: Tên cập nhật không được trùng phòng khác
        // ──────────────────────────────────────────────────────────────
        public async Task<ApiResponse<RoomDto>> EditAsync(RoomSaveDto dto)
        {
            try
            {
                var error = await ValidateAsync(dto, isEdit: true);
                if (error != null)
                    return ApiResponse<RoomDto>.Fail(error, StatusCodes.Status400BadRequest);

                var entity = await _repository.GetByIdAsync(dto.Id);
                if (entity == null)
                    return ApiResponse<RoomDto>.Fail("ERR_ROOM_NOT_FOUND", StatusCodes.Status404NotFound);

                entity.Code      = dto.Code.Trim();
                entity.Name      = dto.Name.Trim();
                entity.Capacity  = dto.Capacity;
                entity.Status    = dto.Status;
                entity.Building  = dto.Building?.Trim();
                entity.Floor     = dto.Floor?.Trim();
                entity.TextSearch = dto.TextSearch;

                await _repository.UpdateAsync(entity);
                await _repository.SaveChangesAsync();

                return ApiResponse<RoomDto>.Ok(MapToDto(entity), "UPDATE_ROOM_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<RoomDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        // ──────────────────────────────────────────────────────────────
        // DELETE (soft-delete)
        // ──────────────────────────────────────────────────────────────
        public async Task<ApiResponse<bool>> DeleteAsync(int id)
        {
            try
            {
                var entity = await _repository.GetByIdAsync(id);
                if (entity == null)
                    return ApiResponse<bool>.Fail("ERR_ROOM_NOT_FOUND", StatusCodes.Status404NotFound);

                await _repository.DeleteAsync(entity);
                await _repository.SaveChangesAsync();

                return ApiResponse<bool>.Ok(true, "DELETE_ROOM_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        // ──────────────────────────────────────────────────────────────
        // DEACTIVE — BR-08: Phòng Inactive, lịch cũ không bị ảnh hưởng
        // ──────────────────────────────────────────────────────────────
        public async Task<ApiResponse<bool>> DeactiveAsync(int id)
        {
            try
            {
                var entity = await _repository.GetByIdAsync(id);
                if (entity == null)
                    return ApiResponse<bool>.Fail("ERR_ROOM_NOT_FOUND", StatusCodes.Status404NotFound);

                // Chỉ chuyển Status sang Inactive, không xóa soft-delete
                // Các lịch cũ đã gán giữ nguyên (BR-08)
                entity.Status = (int)RoomStatus.Inactive;
                await _repository.UpdateAsync(entity);
                await _repository.SaveChangesAsync();

                return ApiResponse<bool>.Ok(true, "DEACTIVATE_ROOM_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        // ──────────────────────────────────────────────────────────────
        // STATS — Overview Cards: Total / Available / InUse / Maintenance
        // ──────────────────────────────────────────────────────────────
        public async Task<ApiResponse<RoomStatsDto>> GetStatsAsync()
        {
            try
            {
                var query = _repository.FindAll();

                var today = DateTime.UtcNow.Date;

                // Đếm tổng, theo status
                var totalRooms       = await query.CountAsync();
                var availableRooms   = await query.CountAsync(r => r.Status == (int)RoomStatus.Active);
                var maintenanceRooms = await query.CountAsync(r => r.Status == (int)RoomStatus.Maintaince);

                // Phòng "đang sử dụng hôm nay" = phòng có ClassSchedule với ngày hôm nay và status OnGoing
                var inUseRooms = await query
                    .Where(r => r.ClassSchedules.Any(cs =>
                        cs.ScheduleDate.HasValue &&
                        cs.ScheduleDate.Value.Date == today &&
                        cs.Status == (int)ClassScheduleStatus.OnGoing))
                    .CountAsync();

                var stats = new RoomStatsDto
                {
                    TotalRooms       = totalRooms,
                    AvailableRooms   = availableRooms,
                    InUseRooms       = inUseRooms,
                    MaintenanceRooms = maintenanceRooms
                };

                return ApiResponse<RoomStatsDto>.Ok(stats, "GET_ROOM_STATS_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<RoomStatsDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        // ──────────────────────────────────────────────────────────────
        // GET SCHEDULE — Lịch sử dụng phòng (ClassSchedule + ExamSchedule)
        // ──────────────────────────────────────────────────────────────
        public async Task<ApiResponse<PagingResponse<RoomScheduleDto>>> GetScheduleAsync(int roomId, BaseSearchDto searchDto)
        {
            try
            {
                var entity = await _repository.GetByIdAsync(roomId);
                if (entity == null)
                    return ApiResponse<PagingResponse<RoomScheduleDto>>.Fail("ERR_ROOM_NOT_FOUND", StatusCodes.Status404NotFound);

                // Load ClassSchedules
                var classSchedules = await _repository.FindAll()
                    .Where(r => r.Id == roomId)
                    .SelectMany(r => r.ClassSchedules)
                    .Include(cs => cs.Class)
                    .Include(cs => cs.TimeSlot)
                    .OrderByDescending(cs => cs.ScheduleDate)
                    .ToListAsync();

                // Load ExamSchedules
                var examSchedules = await _repository.FindAll()
                    .Where(r => r.Id == roomId)
                    .SelectMany(r => r.ExamSchedules)
                    .Include(es => es.Exam)
                    .Include(es => es.TimeSlot)
                    .OrderByDescending(es => es.ExamDate)
                    .ToListAsync();

                // Map sang RoomScheduleDto
                var allSchedules = new List<RoomScheduleDto>();

                allSchedules.AddRange(classSchedules.Select(cs => new RoomScheduleDto
                {
                    ScheduleId   = cs.Id,
                    ScheduleType = "ClassSchedule",
                    ClassName    = cs.Class?.Name,
                    SlotName     = cs.TimeSlot?.Name,
                    SlotTime     = cs.TimeSlot != null
                        ? $"{cs.TimeSlot.StartTime:hh\\:mm} - {cs.TimeSlot.EndTime:hh\\:mm}"
                        : null,
                    ScheduleDate = cs.ScheduleDate,
                    Status       = cs.Status,
                    StatusName   = ((ClassScheduleStatus)cs.Status).GetStringValue(),
                    Note         = cs.Note
                }));

                allSchedules.AddRange(examSchedules.Select(es => new RoomScheduleDto
                {
                    ScheduleId   = es.Id,
                    ScheduleType = "ExamSchedule",
                    ClassName    = es.Exam?.Name,
                    SlotName     = es.TimeSlot?.Name,
                    SlotTime     = es.TimeSlot != null
                        ? $"{es.TimeSlot.StartTime:hh\\:mm} - {es.TimeSlot.EndTime:hh\\:mm}"
                        : null,
                    ScheduleDate = es.ExamDate,
                    Status       = es.Status,
                    StatusName   = ((ExamScheduleStatus)es.Status).GetStringValue(),
                    Note         = es.Note
                }));

                // Sắp xếp chung theo ngày giảm dần
                allSchedules = allSchedules.OrderByDescending(s => s.ScheduleDate).ToList();

                // Phân trang thủ công
                var totalRecords = allSchedules.Count;
                var items = allSchedules
                    .Skip((searchDto.PageIndex - 1) * searchDto.PageSize)
                    .Take(searchDto.PageSize);

                var pagingResponse = items.ToPagingResponse(totalRecords, searchDto);

                return ApiResponse<PagingResponse<RoomScheduleDto>>.Ok(pagingResponse, "GET_ROOM_SCHEDULE_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<PagingResponse<RoomScheduleDto>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        // ──────────────────────────────────────────────────────────────
        // PRIVATE HELPERS
        // ──────────────────────────────────────────────────────────────
        private static RoomDto MapToDto(Room entity) => new()
        {
            Id          = entity.Id,
            Code        = entity.Code ?? string.Empty,
            Name        = entity.Name ?? string.Empty,
            Capacity    = entity.Capacity,
            Status      = entity.Status,
            StatusName  = ((RoomStatus)entity.Status).GetStringValue(),
            Building    = entity.Building,
            Floor       = entity.Floor
        };

        /// <summary>
        /// Validate chung cho Create và Edit.
        /// BR-11: Tên phòng phải unique.
        /// </summary>
        private async Task<string?> ValidateAsync(RoomSaveDto dto, bool isEdit)
        {
            if (string.IsNullOrWhiteSpace(dto.Code))
                return "ERR_CODE_EMPTY";

            if (dto.Code.Length > 50)
                return "ERR_CODE_MAX_LENGTH";

            if (string.IsNullOrWhiteSpace(dto.Name))
                return "ERR_NAME_EMPTY";

            if (dto.Name.Length > 200)
                return "ERR_NAME_MAX_LENGTH";

            if (!dto.Capacity.HasValue)
                return "ERR_CAPACITY_EMPTY";

            if (dto.Capacity.HasValue && dto.Capacity < 1)
                return "ERR_CAPACITY_INVALID";

            var (codeExists, nameExists) = await ValidationHelper.CheckDuplicateCodeAndNameAsync(_repository, isEdit ? dto.Id : (int?)null, dto.Code, dto.Name);
            if (codeExists)
                return "ERR_CODE_DUPLICATE";

            if (nameExists)
                return "ERR_NAME_DUPLICATE";

            return null;
        }

        public async Task<ApiResponse<List<RoomDto>>> GetAvailableRoomsAsync(AvailableRoomFilterDto filterDto)
        {
            try
            {
                filterDto ??= new AvailableRoomFilterDto();

                var query = _repository.FindAll()
                    .Where(r => r.Status == (int)RoomStatus.Active && !r.IsDeleted);

                // 1. Check Capacity
                int? minCapacity = filterDto.MinCapacity;
                if (!minCapacity.HasValue && filterDto.ClassId.HasValue)
                {
                    var cls = await _classRepository.FindAll()
                        .Include(c => c.StudentClasses)
                        .FirstOrDefaultAsync(c => c.Id == filterDto.ClassId.Value && !c.IsDeleted);
                    if (cls != null && cls.StudentClasses != null)
                    {
                        minCapacity = cls.StudentClasses.Count;
                    }
                }

                if (minCapacity.HasValue && minCapacity.Value > 0)
                {
                    query = query.Where(r => r.Capacity >= minCapacity.Value);
                }

                var rooms = await query.ToListAsync();

                // 2. Check Schedule Conflict on specific Date + Slot
                if (filterDto.Date.HasValue && filterDto.SlotIndex.HasValue)
                {
                    var targetDate = filterDto.Date.Value.Date;
                    var fixedSlots = FixedTimeSlot.All;
                    if (filterDto.SlotIndex.Value >= 0 && filterDto.SlotIndex.Value < fixedSlots.Length)
                    {
                        var targetSlot = fixedSlots[filterDto.SlotIndex.Value];

                        var busySchedules = await _scheduleRepository.FindAll()
                            .Include(cs => cs.TimeSlot)
                            .Where(cs => cs.ScheduleDate.HasValue
                                      && cs.ScheduleDate.Value.Date == targetDate
                                      && cs.Status != (int)ClassScheduleStatus.Cancelled
                                      && cs.RoomId.HasValue
                                      && (!filterDto.ExcludeScheduleId.HasValue || cs.Id != filterDto.ExcludeScheduleId.Value)
                                      && (!filterDto.ExcludeClassId.HasValue || cs.ClassId != filterDto.ExcludeClassId.Value))
                            .ToListAsync();

                        var occupiedRoomIds = new HashSet<int>();
                        foreach (var bs in busySchedules)
                        {
                            if (bs.TimeSlot != null && bs.RoomId.HasValue)
                            {
                                if (bs.TimeSlot.StartTime < targetSlot.End && bs.TimeSlot.EndTime > targetSlot.Start)
                                {
                                    occupiedRoomIds.Add(bs.RoomId.Value);
                                }
                            }
                        }

                        rooms = rooms.Where(r => !occupiedRoomIds.Contains(r.Id)).ToList();
                    }
                }

                // 3. Check Schedule Conflict across Date Range for Weekly Schedules
                if (!string.IsNullOrWhiteSpace(filterDto.WeeklySchedulesJson) && filterDto.StartDate.HasValue && filterDto.EndDate.HasValue)
                {
                    try
                    {
                        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        var weeklyItems = JsonSerializer.Deserialize<List<WeeklyScheduleFilterItem>>(filterDto.WeeklySchedulesJson, options);
                        if (weeklyItems != null && weeklyItems.Any())
                        {
                            var rangeStart = filterDto.StartDate.Value.Date;
                            var rangeEnd = filterDto.EndDate.Value.Date;

                            var existingSchedules = await _scheduleRepository.FindAll()
                                .Include(cs => cs.TimeSlot)
                                .Where(cs => cs.ScheduleDate.HasValue
                                          && cs.ScheduleDate.Value.Date >= rangeStart
                                          && cs.ScheduleDate.Value.Date <= rangeEnd
                                          && cs.Status != (int)ClassScheduleStatus.Cancelled
                                          && cs.RoomId.HasValue
                                          && (!filterDto.ExcludeClassId.HasValue || cs.ClassId != filterDto.ExcludeClassId.Value))
                                .ToListAsync();

                            var occupiedRoomIds = new HashSet<int>();
                            foreach (var ws in weeklyItems)
                            {
                                if (TimeSpan.TryParse(ws.StartTime, out var wStart) && TimeSpan.TryParse(ws.EndTime, out var wEnd))
                                {
                                    foreach (var es in existingSchedules)
                                    {
                                        if ((int)es.ScheduleDate!.Value.DayOfWeek == ws.DayOfWeek && es.TimeSlot != null && es.RoomId.HasValue)
                                        {
                                            if (es.TimeSlot.StartTime < wEnd && es.TimeSlot.EndTime > wStart)
                                            {
                                                occupiedRoomIds.Add(es.RoomId.Value);
                                            }
                                        }
                                    }
                                }
                            }

                            rooms = rooms.Where(r => !occupiedRoomIds.Contains(r.Id)).ToList();
                        }
                    }
                    catch
                    {
                        // Ignore json parse error and proceed
                    }
                }

                var dtoList = rooms
                    .OrderBy(r => r.Capacity)
                    .ThenBy(r => r.Name)
                    .Select(r => new RoomDto
                    {
                        Id = r.Id,
                        Code = r.Code,
                        Name = r.Name,
                        Building = r.Building,
                        Floor = r.Floor,
                        Capacity = r.Capacity,
                        Status = r.Status,
                        StatusName = ((RoomStatus)r.Status).ToString()
                    })
                    .ToList();

                return ApiResponse<List<RoomDto>>.Ok(dtoList, "GET_AVAILABLE_ROOMS_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<List<RoomDto>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        private class WeeklyScheduleFilterItem
        {
            public int DayOfWeek { get; set; }
            public string StartTime { get; set; } = string.Empty;
            public string EndTime { get; set; } = string.Empty;
        }
    }
}

