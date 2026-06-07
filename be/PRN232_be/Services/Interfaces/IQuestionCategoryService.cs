using PRN232_be.DTO;
using PRN232_be.DTO.QuestionCategory;

namespace PRN232_be.Services.Interfaces
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
