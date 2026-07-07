using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using sep490_be.DTO.Class;

namespace sep490_be.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestController : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            return Ok("Heheh123");
        }
    }
}

