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
        Task<ApiResponse<ConflictCheckResultDto>> CheckConflictAsync(ClassSaveDto dto);
    }
}

