using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Mapster;
using PRN232_be.DTO;
using PRN232_be.DTO.Student;
using PRN232_be.Models;
using PRN232_be.Enums;
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

                // Keyword search using TextSearch field
                if (!string.IsNullOrWhiteSpace(searchDto.Keyword))
                {
                    query = query.Where(s => s.TextSearch != null && s.TextSearch.Contains(searchDto.Keyword));
                }

                // Filtering by StudentStatus
                if (searchDto.StudentStatus.HasValue)
                {
                    query = query.Where(s => s.Status == searchDto.StudentStatus.Value);
                }

                // Filtering by GradeLevel
                if (searchDto.GradeLevel.HasValue)
                {
                    query = query.Where(s => s.GradeLevel == searchDto.GradeLevel.Value);
                }

                // Filtering by Gender
                if (searchDto.Gender.HasValue)
                {
                    query = query.Where(s => s.Gender == searchDto.Gender.Value);
                }

                var totalRecords = await query.CountAsync();
                var entities = await query.ApplyPagingAsync(searchDto);

                var dtos = entities.Select(MapToDto).ToList();
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

        public async Task<ApiResponse<StudentDto>> CreateAsync(StudentSaveDto dto)
        {
            try
            {
                var validationError = await ValidateAsync(dto, isEdit: false);
                if (validationError != null)
                {
                    return ApiResponse<StudentDto>.Fail(validationError, StatusCodes.Status400BadRequest);
                }

                var entity = dto.Adapt<Student>();
                entity.Id = 0;

                await _repository.AddAsync(entity);
                await _repository.SaveChangesAsync();

                return ApiResponse<StudentDto>.Created(MapToDto(entity), "CREATE_STUDENT_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<StudentDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<StudentDto>> EditAsync(StudentSaveDto dto)
        {
            try
            {
                var validationError = await ValidateAsync(dto, isEdit: true);
                if (validationError != null)
                {
                    return ApiResponse<StudentDto>.Fail(validationError, StatusCodes.Status400BadRequest);
                }

                var existingEntity = await _repository.GetByIdAsync(dto.Id);
                if (existingEntity == null)
                {
                    return ApiResponse<StudentDto>.Fail("ERR_STUDENT_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                dto.Adapt(existingEntity);

                await _repository.UpdateAsync(existingEntity);
                await _repository.SaveChangesAsync();

                return ApiResponse<StudentDto>.Ok(MapToDto(existingEntity), "UPDATE_STUDENT_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<StudentDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<bool>> DeleteAsync(int id)
        {
            try
            {
                var existingEntity = await _repository.GetByIdAsync(id);
                if (existingEntity == null)
                {
                    return ApiResponse<bool>.Fail("ERR_STUDENT_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                await _repository.DeleteAsync(existingEntity);
                await _repository.SaveChangesAsync();

                return ApiResponse<bool>.Ok(true, "DELETE_STUDENT_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<bool>> DeactiveAsync(int id)
        {
            try
            {
                var existingEntity = await _repository.GetByIdAsync(id);
                if (existingEntity == null)
                {
                    return ApiResponse<bool>.Fail("ERR_STUDENT_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                await _repository.DeactiveAsync(existingEntity);
                await _repository.SaveChangesAsync();

                return ApiResponse<bool>.Ok(true, "DEACTIVATE_STUDENT_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        // ===================== PRIVATE MAPPING =====================

        private static StudentDto MapToDto(Student entity)
        {
            string statusName = "Unknown";
            if (Enum.IsDefined(typeof(StudentStatus), entity.Status))
            {
                statusName = ((StudentStatus)entity.Status).GetStringValue();
            }

            return new StudentDto
            {
                Id = entity.Id,
                Code = entity.Code ?? string.Empty,
                Name = entity.Name ?? string.Empty,
                Dob = entity.Dob,
                Gender = entity.Gender,
                Email = entity.Email,
                Phone = entity.Phone,
                Address = entity.Address,
                Status = entity.Status,
                StatusName = statusName,
                Description = entity.Description,
                SchoolName = entity.SchoolName,
                GradeLevel = entity.GradeLevel,
                ParentName = entity.ParentName,
                ParentPhone = entity.ParentPhone,
                Avatar = entity.Avatar
            };
        }

        // ===================== PRIVATE VALIDATE =====================

        private async Task<string?> ValidateAsync(StudentSaveDto dto, bool isEdit)
        {
            if (string.IsNullOrWhiteSpace(dto.Code))
                return "ERR_CODE_EMPTY";

            if (dto.Code.Length > 50)
                return "ERR_CODE_MAX_LENGTH";

            if (string.IsNullOrWhiteSpace(dto.Name))
                return "ERR_NAME_EMPTY";

            if (dto.Name.Length > 200)
                return "ERR_NAME_MAX_LENGTH";

            if (dto.Email != null && dto.Email.Length > 150)
                return "ERR_EMAIL_MAX_LENGTH";

            if (dto.Phone != null && dto.Phone.Length > 20)
                return "ERR_PHONE_MAX_LENGTH";

            if (dto.Address != null && dto.Address.Length > 500)
                return "ERR_ADDRESS_MAX_LENGTH";

            if (dto.SchoolName != null && dto.SchoolName.Length > 200)
                return "ERR_SCHOOL_NAME_MAX_LENGTH";

            if (dto.ParentName != null && dto.ParentName.Length > 200)
                return "ERR_PARENT_NAME_MAX_LENGTH";

            if (dto.ParentPhone != null && dto.ParentPhone.Length > 20)
                return "ERR_PARENT_PHONE_MAX_LENGTH";

            // Check duplicate Code
            var duplicateCode = await _repository.FindAll()
                .FirstOrDefaultAsync(s => s.Code == dto.Code && (!isEdit || s.Id != dto.Id));

            if (duplicateCode != null)
                return "ERR_CODE_DUPLICATE";

            return null;
        }
    }
}
