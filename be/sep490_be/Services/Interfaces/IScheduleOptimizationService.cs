using System.Collections.Generic;
using System.Threading.Tasks;
using sep490_be.DTO;
using sep490_be.DTO.Class;

namespace sep490_be.Services.Interfaces
{
    public interface IScheduleOptimizationService
    {
        Task<ApiResponse<List<ClassDto>>> AutoScheduleAsync(List<int> classIds, AutoScheduleConstraintDto constraints);
        Task<ApiResponse<List<ClassDto>>> AutoScheduleSemesterAsync(AutoScheduleSemesterRequestDto request);
        Task<ApiResponse<List<ClassDto>>> SaveSemesterScheduleDraftAsync(SaveScheduleDraftRequestDto request);
        Task<ApiResponse<ScheduleVersionListItemDto>> SaveScheduleVersionAsync(int semesterId, string name);
        Task<ApiResponse<List<ScheduleVersionListItemDto>>> GetScheduleVersionsAsync(int semesterId);
        Task<ApiResponse<bool>> DeleteScheduleVersionAsync(int versionId);
        Task<ApiResponse<List<ClassDto>>> GetScheduleVersionPreviewAsync(int versionId);
        Task PurgeScheduleVersionsIfSemesterEmptyAsync(int semesterId);
        Task<ApiResponse<List<ClassDto>>> RollbackSemesterScheduleAsync(int semesterId, int versionId);
        Task<ApiResponse<ConflictCheckResultDto>> CheckConflictAsync(ClassSaveDto dto);
    }
}

