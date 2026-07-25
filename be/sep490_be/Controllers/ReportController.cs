using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using sep490_be.Services.Interfaces;
using System.Threading.Tasks;
using sep490_be.DTO.Common;
using sep490_be.Helpers.Authorization;

namespace sep490_be.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ReportController : ControllerBase
    {
        private readonly IReportService _reportService;

        public ReportController(IReportService reportService)
        {
            _reportService = reportService;
        }

        [HttpGet("AttendanceSheet/{classId}")]
        [HasPermission(Permissions.AttendanceReport.AttendanceReportPage)]
        public async Task<IActionResult> GetClassAttendanceSheet(int classId)
        {
            var response = await _reportService.GetClassAttendanceSheetAsync(classId);
            if (!response.Success)
            {
                return StatusCode(response.StatusCode, response);
            }
            return Ok(response);
        }

        [HttpGet("ExamResult/{examId}")]
        [HasPermission(Permissions.ExamReport.ExamReportPage)]
        public async Task<IActionResult> GetExamResultAnalysis(int examId)
        {
            var response = await _reportService.GetExamResultAnalysisAsync(examId);
            if (!response.Success)
            {
                return StatusCode(response.StatusCode, response);
            }
            return Ok(response);
        }

        [HttpGet("ClassGrades/{classId}")]
        [HasPermission(Permissions.ClassGradeReport.ClassGradeReportPage)]
        public async Task<IActionResult> GetClassGradeReport(int classId)
        {
            var response = await _reportService.GetClassGradeReportAsync(classId);
            if (!response.Success)
            {
                return StatusCode(response.StatusCode, response);
            }
            return Ok(response);
        }
    }
}
