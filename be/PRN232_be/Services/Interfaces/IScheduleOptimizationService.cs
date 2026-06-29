using System.Collections.Generic;
using System.Threading.Tasks;
using PRN232_be.DTO;
using PRN232_be.DTO.Class;

namespace PRN232_be.Services.Interfaces
{
    public interface IScheduleOptimizationService
    {
        Task<ApiResponse<List<ClassDto>>> AutoScheduleAsync(List<int> classIds);
        Task<ApiResponse<ConflictCheckResultDto>> CheckConflictAsync(ClassSaveDto dto);
    }
}
