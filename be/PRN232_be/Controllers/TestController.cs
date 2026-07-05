using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PRN232_be.DTO.Class;

namespace PRN232_be.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestController : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            return Ok("Heheh");
        }
    }
}
