using System.Collections.Generic;
using System.Threading.Tasks;
using PRN232_be.DTO;
using PRN232_be.DTO.LearningMaterial;

namespace PRN232_be.Services.Interfaces
{
    public interface ILearningMaterialService
    {
        Task<ApiResponse<PagingResponse<LearningMaterialDto>>> GetAllMaterialsAsync(LearningMaterialSearchDto searchDto, string username, IList<string> roles);
        Task<ApiResponse<LearningMaterialDto>> GetMaterialByIdAsync(int id);
        Task<ApiResponse<LearningMaterialDto>> CreateMaterialAsync(LearningMaterialSaveDto dto, string username);
        Task<ApiResponse<LearningMaterialDto>> EditMaterialAsync(LearningMaterialSaveDto dto, string username, IList<string> roles);
        Task<ApiResponse<bool>> DeleteMaterialAsync(int id, string username, IList<string> roles);
        Task<ApiResponse<bool>> DeactiveMaterialAsync(int id, string username, IList<string> roles);
    }
}
