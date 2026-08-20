using System.Collections.Generic;
using sep490_be.DTO;
using sep490_be.DTO.Class;

namespace sep490_be.Services.Interfaces
{
    public interface IClassService
    {
        Task<ApiResponse<PagingResponse<ClassDto>>> GetAllAsync(ClassSearchDto searchDto);
        Task<ApiResponse<ClassDto>> GetByIdAsync(int id, string? username = null);
        Task<ApiResponse<ClassDto>> CreateAsync(ClassSaveDto dto);
        Task<ApiResponse<ClassDto>> EditAsync(ClassSaveDto dto);
        Task<ApiResponse<bool>> DeleteAsync(int id);
        Task<ApiResponse<bool>> DeactiveAsync(int id);
        Task<ApiResponse<List<ClassScheduleDto>>> GetTeacherSchedulesAsync(string username);
        Task<ApiResponse<List<ClassScheduleDto>>> GetStudentSchedulesAsync(string username);
        Task<ApiResponse<List<ClassScheduleDto>>> GetChildSchedulesAsync(string username, int studentId);
        Task<ApiResponse<List<ClassScheduleDto>>> GetClassSchedulesAsync();
        Task<ApiResponse<PagingResponse<ClassDto>>> GetTeacherClassesAsync(string username, ClassSearchDto searchDto);
        Task<ApiResponse<PagingResponse<ClassDto>>> GetStudentClassesAsync(string username, ClassSearchDto searchDto);
        Task<ApiResponse<PagingResponse<ClassDto>>> GetAccessibleClassesAsync(string username, ClassSearchDto searchDto);
        Task<ApiResponse<ClassScheduleDto>> UpdateScheduleSlotAsync(int id, UpdateScheduleSlotDto dto);
        Task<ApiResponse<MoveScheduleSlotResultDto>> MoveScheduleSlotAsync(int id, MoveScheduleSlotDto dto);
    }
}

