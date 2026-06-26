using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PRN232_be.DTO.Room;
using PRN232_be.DTO.Common;
using PRN232_be.Services.Interfaces;
using PRN232_be.Helpers.Authorization;

namespace PRN232_be.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class RoomController : ControllerBase
    {
        private readonly IRoomService _service;

        public RoomController(IRoomService service)
        {
            _service = service;
        }

        // GET: api/Room
        [HttpGet]
        [HasPermission(Permissions.Room.Room_View)]
        public async Task<IActionResult> GetAll([FromQuery] RoomSearchDto searchDto)
        {
            var response = await _service.GetAllAsync(searchDto);
            return StatusCode(response.StatusCode, response);
        }

        // GET: api/Room/5
        [HttpGet("{id}")]
        [HasPermission(Permissions.Room.Room_View)]
        public async Task<IActionResult> GetById(int id)
        {
            var response = await _service.GetByIdAsync(id);
            return StatusCode(response.StatusCode, response);
        }
    }
}
