using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using sep490_be.DTO;
using sep490_be.DTO.Common;
using sep490_be.DTO.Room;
using sep490_be.Helpers.Authorization;
using sep490_be.Services.Interfaces;

namespace sep490_be.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class RoomController : ControllerBase
    {
        private readonly IRoomService _roomService;

        public RoomController(IRoomService roomService)
        {
            _roomService = roomService;
        }

        // ──────────────────────────────────────────────────────────────
        // GET api/Room
        // ──────────────────────────────────────────────────────────────
        /// <summary>Lấy danh sách phòng học (có phân trang, tìm kiếm, lọc)</summary>
        [HttpGet]
        [HasPermission(Permissions.Room.Room_View)]
        public async Task<IActionResult> GetAll([FromQuery] RoomSearchDto searchDto)
        {
            var response = await _roomService.GetAllAsync(searchDto);
            return StatusCode(response.StatusCode, response);
        }

        // ──────────────────────────────────────────────────────────────
        // GET api/Room/stats
        // ──────────────────────────────────────────────────────────────
        /// <summary>Thống kê tổng quan phòng học: Total / Available / InUse / Maintenance</summary>
        [HttpGet("stats")]
        [HasPermission(Permissions.Room.Room_View)]
        public async Task<IActionResult> GetStats()
        {
            var response = await _roomService.GetStatsAsync();
            return StatusCode(response.StatusCode, response);
        }

        // ──────────────────────────────────────────────────────────────
        // GET api/Room/{id}
        // ──────────────────────────────────────────────────────────────
        /// <summary>Lấy chi tiết một phòng học</summary>
        [HttpGet("{id}")]
        [HasPermission(Permissions.Room.Room_View)]
        public async Task<IActionResult> GetById(int id)
        {
            var response = await _roomService.GetByIdAsync(id);
            return StatusCode(response.StatusCode, response);
        }

        // ──────────────────────────────────────────────────────────────
        // GET api/Room/{id}/schedule
        // ──────────────────────────────────────────────────────────────
        /// <summary>Xem lịch sử dụng của phòng học (ClassSchedule + ExamSchedule)</summary>
        [HttpGet("{id}/schedule")]
        [HasPermission(Permissions.Room.Room_View)]
        public async Task<IActionResult> GetSchedule(int id, [FromQuery] BaseSearchDto searchDto)
        {
            var response = await _roomService.GetScheduleAsync(id, searchDto);
            return StatusCode(response.StatusCode, response);
        }

        // ──────────────────────────────────────────────────────────────
        // POST api/Room
        // ──────────────────────────────────────────────────────────────
        /// <summary>
        /// Tạo phòng học mới.
        /// BR-11: Tên phòng phải là duy nhất.
        /// Lưu ý: ảnh phòng (Image) cần upload trước qua POST /api/upload/image để lấy URL rồi truyền vào.
        /// </summary>
        [HttpPost]
        [HasPermission(Permissions.Room.Room_Create)]
        public async Task<IActionResult> Create([FromBody] RoomSaveDto dto)
        {
            var response = await _roomService.CreateAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        // ──────────────────────────────────────────────────────────────
        // PUT api/Room/{id}
        // ──────────────────────────────────────────────────────────────
        /// <summary>
        /// Cập nhật thông tin phòng học.
        /// BR-11: Tên mới không được trùng phòng khác.
        /// </summary>
        [HttpPut("{id}")]
        [HasPermission(Permissions.Room.Room_Edit)]
        public async Task<IActionResult> Edit(int id, [FromBody] RoomSaveDto dto)
        {
            dto.Id = id;
            var response = await _roomService.EditAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        // ──────────────────────────────────────────────────────────────
        // DELETE api/Room/{id}
        // ──────────────────────────────────────────────────────────────
        /// <summary>Xóa mềm phòng học</summary>
        [HttpDelete("{id}")]
        [HasPermission(Permissions.Room.Room_Delete)]
        public async Task<IActionResult> Delete(int id)
        {
            var response = await _roomService.DeleteAsync(id);
            return StatusCode(response.StatusCode, response);
        }

        // ──────────────────────────────────────────────────────────────
        // POST api/Room/{id}/deactive
        // ──────────────────────────────────────────────────────────────
        /// <summary>
        /// Vô hiệu hóa phòng học (chuyển sang Inactive).
        /// BR-08: Các lịch học hiện tại gán với phòng này không bị ảnh hưởng.
        /// </summary>
        [HttpPost("{id}/deactive")]
        [HasPermission(Permissions.Room.Room_Delete)]
        public async Task<IActionResult> Deactive(int id)
        {
            var response = await _roomService.DeactiveAsync(id);
            return StatusCode(response.StatusCode, response);
        }
    }
}

