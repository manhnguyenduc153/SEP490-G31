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
        Task<ApiResponse<List<MyAttendanceClassDto>>> GetMyAttendanceAsync(IEnumerable<string> identifiers);
        Task<ApiResponse<List<MyAttendanceSessionDto>>> GetMyAttendanceDetailsAsync(int classId, IEnumerable<string> identifiers);
    }
}

