using System.Threading.Tasks;
using System.Collections.Generic;
using sep490_be.DTO;
using sep490_be.DTO.Exam;

namespace sep490_be.Services.Interfaces
{
    public interface IExamService
    {
        Task<ApiResponse<PagingResponse<ExamDto>>> GetAllAsync(ExamSearchDto searchDto);
        Task<ApiResponse<PagingResponse<ExamDto>>> GetTeacherExamsAsync(ExamSearchDto searchDto, string teacherEmailOrCode);
        Task<ApiResponse<ExamDto>> GetByIdAsync(int id);
        Task<ApiResponse<ExamDto>> CreateAsync(ExamSaveDto dto);
        Task<ApiResponse<ExamDto>> EditAsync(ExamSaveDto dto);
        Task<ApiResponse<bool>> DeleteAsync(int id);
        Task<ApiResponse<ExamDto>> ToggleStatusAsync(int id);
        Task<ApiResponse<ExamDto>> CopyAsync(int id);
        Task<ApiResponse<List<ExamAttemptDto>>> GetAttemptsByExamAsync(int examId);
        Task<ApiResponse<List<ExamAttemptDto>>> GetStudentAttemptsAsync(int examId, string userEmailOrCode);
        Task<ApiResponse<List<ExamDto>>> GetStudentExamsAsync(string userEmailOrCode);
        Task<ApiResponse<ExamDto>> GetStudentExamDetailAsync(int examId, string userEmailOrCode);
        Task<ApiResponse<ExamAttemptDto>> StartAttemptAsync(int examId, string userEmailOrCode);
        Task<ApiResponse<ExamAttemptDto>> SubmitAttemptAsync(int examId, ExamSubmitDto submitDto, string userEmailOrCode);
        Task<ApiResponse<bool>> SaveProgressAsync(int examId, ExamSubmitDto dto, string userEmailOrCode);
        Task<ApiResponse<ExamAttemptDto>> GradeAttemptAsync(int attemptId, GradeAttemptDto gradeDto, string teacherEmailOrCode);
    }
}

