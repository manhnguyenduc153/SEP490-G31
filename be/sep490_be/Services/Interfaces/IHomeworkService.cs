using System.Collections.Generic;
using System.Threading.Tasks;
using sep490_be.DTO.Homework;
using sep490_be.DTO;
using sep490_be.Helpers;

namespace sep490_be.Services.Interfaces
{
    public interface IHomeworkService
    {
        Task<ApiResponse<IEnumerable<HomeworkDto>>> GetHomeworkByClassAsync(int classId, string? username, bool isStudent);
        Task<ApiResponse<IEnumerable<HomeworkDto>>> GetStudentHomeworkByClassAsync(int classId, string? username);
        Task<ApiResponse<HomeworkDto>> CreateHomeworkAsync(HomeworkSaveDto dto);
        Task<ApiResponse<HomeworkDto>> UpdateHomeworkAsync(int id, HomeworkSaveDto dto);
        Task<ApiResponse<bool>> DeleteHomeworkAsync(int id);
        
        Task<ApiResponse<IEnumerable<HomeworkSubmissionDto>>> GetSubmissionsByHomeworkAsync(int homeworkId);
        Task<ApiResponse<HomeworkSubmissionDto>> SubmitHomeworkAsync(HomeworkSubmissionSaveDto dto, string? username);
        Task<ApiResponse<HomeworkSubmissionDto>> GradeSubmissionAsync(int submissionId, HomeworkSubmissionGradeDto dto);
        Task<ApiResponse<HomeworkSubmissionDto?>> GetMySubmissionAsync(int homeworkId, string? username);
    }
}
