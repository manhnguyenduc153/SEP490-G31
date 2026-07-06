using System.Collections.Generic;
using System.Threading.Tasks;
using sep490_be.DTO;
using sep490_be.DTO.Attendance;

namespace sep490_be.Services.Interfaces
{
    public interface IAttendanceService
    {
        Task<ApiResponse<List<AttendanceDto>>> GetByScheduleIdAsync(int scheduleId);
        Task<ApiResponse<bool>> BulkSaveAsync(AttendanceBulkSaveDto dto);
        Task<ApiResponse<AttendanceReportDto>> GetReportByClassIdAsync(int classId);
    }
}

