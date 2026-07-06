using sep490_be.DTO;
using sep490_be.DTO.QuestionCategory;

namespace sep490_be.Services.Interfaces
{
    public interface IQuestionCategoryService
    {
        Task<ApiResponse<PagingResponse<QuestionCategoryDto>>> GetAllAsync(QuestionCategorySearchDto searchDto);
        Task<ApiResponse<QuestionCategoryDto>> GetByIdAsync(int id);
        Task<ApiResponse<QuestionCategoryDto>> CreateAsync(QuestionCategorySaveDto dto);
        Task<ApiResponse<QuestionCategoryDto>> EditAsync(QuestionCategorySaveDto dto);
        Task<ApiResponse<bool>> DeleteAsync(int id);
        Task<ApiResponse<bool>> DeactiveAsync(int id);
    }
}

