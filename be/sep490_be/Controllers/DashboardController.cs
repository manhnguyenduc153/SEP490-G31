using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using sep490_be.Services.Interfaces;
using sep490_be.DTO.Common;
using sep490_be.Helpers.Authorization;

namespace sep490_be.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;

        public DashboardController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        [HttpGet]
        [HasPermission(Permissions.Dashboard.DashboardPage)]
        public async Task<IActionResult> GetDashboardData()
        {
            var result = await _dashboardService.GetDashboardDataAsync();
            return StatusCode(result.StatusCode, result);
        }
    }
}
