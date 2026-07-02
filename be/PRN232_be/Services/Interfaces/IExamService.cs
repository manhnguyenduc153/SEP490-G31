using System.Threading.Tasks;
using System.Collections.Generic;
using PRN232_be.DTO;
using PRN232_be.DTO.Exam;

namespace PRN232_be.Services.Interfaces
{
    public interface IExamService
    {
        Task<ApiResponse<PagingResponse<ExamDto>>> GetAllAsync(ExamSearchDto searchDto);
        Task<ApiResponse<ExamDto>> GetByIdAsync(int id);
        Task<ApiResponse<ExamDto>> CreateAsync(ExamSaveDto dto);
        Task<ApiResponse<ExamDto>> EditAsync(ExamSaveDto dto);
        Task<ApiResponse<bool>> DeleteAsync(int id);
        Task<ApiResponse<ExamDto>> CopyAsync(int id);
    }
}
