using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PRN232_be.DTO;
using PRN232_be.DTO.Student;
using PRN232_be.Models;
using PRN232_be.Repositories.Interfaces;
using PRN232_be.Services.Interfaces;
using PRN232_be.Helpers;

namespace PRN232_be.Services.Implementations
{
    public class StudentService : IStudentService
    {
        private readonly IStudentRepository _repository;

        public StudentService(IStudentRepository repository)
        {
            _repository = repository;
        }

        public async Task<ApiResponse<PagingResponse<StudentDto>>> GetAllAsync(StudentSearchDto searchDto)
        {
            try
            {
                var query = _repository.FindAll();

                if (!string.IsNullOrWhiteSpace(searchDto.Keyword))
                {
                    query = query.Where(c => c.TextSearch != null && c.TextSearch.Contains(searchDto.Keyword));
                }

                // If PageSize is set to -1 or very large, return all matching records
                var totalRecords = await query.CountAsync();
                List<Student> entities;
                if (searchDto.PageSize <= 0)
                {
                    entities = await query.ToListAsync();
                }
                else
                {
                    entities = await query.ApplyPagingAsync(searchDto);
                }

                var dtos = entities.Select(MapToDto);
                var pagingResponse = dtos.ToPagingResponse(totalRecords, searchDto);

                return ApiResponse<PagingResponse<StudentDto>>.Ok(pagingResponse, "GET_STUDENT_LIST_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<PagingResponse<StudentDto>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<StudentDto>> GetByIdAsync(int id)
        {
            try
            {
                var entity = await _repository.GetByIdAsync(id);
                if (entity == null)
                {
                    return ApiResponse<StudentDto>.Fail("ERR_STUDENT_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                return ApiResponse<StudentDto>.Ok(MapToDto(entity), "GET_STUDENT_DETAIL_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<StudentDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        private static StudentDto MapToDto(Student entity) => new()
        {
            Id = entity.Id,
            Code = entity.Code ?? string.Empty,
            Name = entity.Name ?? string.Empty,
            Email = entity.Email,
            Phone = entity.Phone
        };
    }
}
