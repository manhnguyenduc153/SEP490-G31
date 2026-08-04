using sep490_be.DTO;
using sep490_be.DTO.QuestionPassage;

namespace sep490_be.Services.Interfaces
{
    public interface IQuestionPassageService
    {
        Task<ApiResponse<PagingResponse<QuestionPassageDto>>> GetAllAsync(QuestionPassageSearchDto searchDto);
        Task<ApiResponse<QuestionPassageDto>> GetByIdAsync(int id);
        Task<ApiResponse<QuestionPassageDto>> CreateAsync(QuestionPassageSaveDto dto);
        Task<ApiResponse<QuestionPassageDto>> EditAsync(QuestionPassageSaveDto dto);
        Task<ApiResponse<bool>> DeleteAsync(int id);
    }
}
