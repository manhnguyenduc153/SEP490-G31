using System.Collections.Generic;
using System.Threading.Tasks;
using PRN232_be.DTO;
using PRN232_be.DTO.Attendance;

namespace PRN232_be.Services.Interfaces
{
    public interface IAttendanceService
    {
        Task<ApiResponse<List<AttendanceDto>>> GetByScheduleIdAsync(int scheduleId);
        Task<ApiResponse<bool>> BulkSaveAsync(AttendanceBulkSaveDto dto);
        Task<ApiResponse<AttendanceReportDto>> GetReportByClassIdAsync(int classId);
    }
}
