using sep490_be.DTO;
using sep490_be.DTO.Dashboard;

namespace sep490_be.Services.Interfaces
{
    public interface IDashboardService
    {
        Task<ApiResponse<DashboardDataDto>> GetDashboardDataAsync();
    }
}
