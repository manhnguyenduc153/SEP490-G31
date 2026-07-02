using System.Collections.Generic;
using System.Threading.Tasks;
using PRN232_be.DTO.Homework;
using PRN232_be.DTO;
using PRN232_be.Helpers;

namespace PRN232_be.Services.Interfaces
{
    public interface IHomeworkService
    {
        Task<ApiResponse<IEnumerable<HomeworkDto>>> GetHomeworkByClassAsync(int classId);
        Task<ApiResponse<HomeworkDto>> CreateHomeworkAsync(HomeworkSaveDto dto);
        Task<ApiResponse<HomeworkDto>> UpdateHomeworkAsync(int id, HomeworkSaveDto dto);
        Task<ApiResponse<bool>> DeleteHomeworkAsync(int id);
        
        Task<ApiResponse<IEnumerable<HomeworkSubmissionDto>>> GetSubmissionsByHomeworkAsync(int homeworkId);
        Task<ApiResponse<HomeworkSubmissionDto>> SubmitHomeworkAsync(HomeworkSubmissionSaveDto dto);
        Task<ApiResponse<HomeworkSubmissionDto>> GradeSubmissionAsync(int submissionId, HomeworkSubmissionGradeDto dto);
    }
}
