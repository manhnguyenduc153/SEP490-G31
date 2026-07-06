using sep490_be.DTO;
using sep490_be.DTO.Common;
using sep490_be.DTO.ParentStudent;

namespace sep490_be.Services.Interfaces
{
    public interface IParentStudentService
    {
        Task<ApiResponse<PagingResponse<ParentStudentDto>>> GetAllAsync(ParentStudentSearchDto searchDto);
        Task<ApiResponse<ParentStudentDto>> GetByIdAsync(int id);

        /// <summary>
        /// Tạo phụ huynh mới → tự động tạo IdentityUser với role "Parent"
        /// </summary>
        Task<ApiResponse<ParentStudentDto>> CreateAsync(ParentStudentSaveDto dto);

        /// <summary>
        /// Cập nhật thông tin phụ huynh (không đổi email/account)
        /// </summary>
        Task<ApiResponse<ParentStudentDto>> EditAsync(ParentStudentSaveDto dto);

        /// <summary>
        /// Soft-delete phụ huynh + lock IdentityUser tương ứng
        /// </summary>
        Task<ApiResponse<bool>> DeleteAsync(int id);

        Task<ApiResponse<bool>> DeactiveAsync(int id);
    }
}

