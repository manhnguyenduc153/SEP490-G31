using sep490_be.DTO;
using sep490_be.DTO.Common;
using sep490_be.DTO.Report;
using System.Threading.Tasks;

namespace sep490_be.Services.Interfaces
{
    public interface IReportService
    {
        Task<ApiResponse<ClassAttendanceSheetDto>> GetClassAttendanceSheetAsync(int classId);
        Task<ApiResponse<ExamResultReportDto>> GetExamResultAnalysisAsync(int examId);
        Task<ApiResponse<ClassGradeReportDto>> GetClassGradeReportAsync(int classId);
    }
}
