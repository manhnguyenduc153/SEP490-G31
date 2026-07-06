using System.Threading.Tasks;
using sep490_be.DTO;
using sep490_be.DTO.Question;

namespace sep490_be.Services.Interfaces
{
    public interface IQuestionService
    {
        Task<ApiResponse<PagingResponse<QuestionDto>>> GetAllAsync(QuestionSearchDto searchDto);
        Task<ApiResponse<QuestionDto>> GetByIdAsync(int id);
        Task<ApiResponse<QuestionDto>> CreateAsync(QuestionSaveDto dto);
        Task<ApiResponse<QuestionDto>> EditAsync(QuestionSaveDto dto);
        Task<ApiResponse<bool>> DeleteAsync(int id);
        Task<ApiResponse<bool>> DeactiveAsync(int id);
        Task<ApiResponse<List<QuestionDto>>> ImportAsync(List<QuestionSaveDto> dtos);
    }
}

