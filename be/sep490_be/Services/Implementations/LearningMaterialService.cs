using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Mapster;
using sep490_be.DTO;
using sep490_be.DTO.LearningMaterial;
using sep490_be.Models;
using sep490_be.Repositories.Interfaces;
using sep490_be.Services.Interfaces;
using sep490_be.Helpers;

namespace sep490_be.Services.Implementations
{
    public class LearningMaterialService : ILearningMaterialService
    {
        private readonly ILearningMaterialRepository _repository;
        private readonly ApplicationDbContext _dbContext;

        public LearningMaterialService(
            ILearningMaterialRepository repository,
            ApplicationDbContext dbContext)
        {
            _repository = repository;
            _dbContext = dbContext;
        }

        public async Task<ApiResponse<PagingResponse<LearningMaterialDto>>> GetAllMaterialsAsync(
            LearningMaterialSearchDto searchDto, 
            string username, 
            IList<string> roles)
        {
            try
            {
                var query = _repository.FindAll()
                    .Include(x => x.Class)
                    .Include(x => x.Course)
                    .Include(x => x.ClassSchedule)
                    .Include(x => x.Teacher)
                    .AsQueryable();


                // Lọc theo keyword (tiêu đề, tên, mã, mô tả thông qua TextSearch)
                if (!string.IsNullOrWhiteSpace(searchDto.Keyword))
                {
                    query = query.Where(x => x.TextSearch != null && x.TextSearch.Contains(searchDto.Keyword));
                }

                // Lọc theo ClassId
                if (searchDto.ClassId.HasValue)
                {
                    query = query.Where(x => x.ClassId == searchDto.ClassId.Value);
                }

                // Lọc theo CourseId
                if (searchDto.CourseId.HasValue)
                {
                    query = query.Where(x => x.CourseId == searchDto.CourseId.Value);
                }

                // Lọc theo ScheduleId
                if (searchDto.ScheduleId.HasValue)
                {
                    query = query.Where(x => x.ScheduleId == searchDto.ScheduleId.Value);
                }

                // Lọc theo UploadedBy (Teacher ID)
                if (searchDto.UploadedBy.HasValue)
                {
                    query = query.Where(x => x.UploadedBy == searchDto.UploadedBy.Value);
                }

                // Lọc theo Status (mặc định lọc các record active nếu searchDto.Status có giá trị)
                if (searchDto.Status.HasValue)
                {
                    query = query.Where(x => x.Status == (searchDto.Status.Value ? 1 : 0));
                }

                var totalRecords = await query.CountAsync();
                
                // Thực hiện phân trang
                var entities = await query
                    .Skip((searchDto.PageIndex - 1) * searchDto.PageSize)
                    .Take(searchDto.PageSize)
                    .ToListAsync();

                var dtos = entities.Select(MapToDto).ToList();
                var pagingResponse = dtos.ToPagingResponse(totalRecords, searchDto);

                return ApiResponse<PagingResponse<LearningMaterialDto>>.Ok(pagingResponse, "GET_LEARNING_MATERIAL_LIST_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<PagingResponse<LearningMaterialDto>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<LearningMaterialDto>> GetMaterialByIdAsync(int id)
        {
            try
            {
                var entity = await _repository.FindAll()
                    .Include(x => x.Class)
                    .Include(x => x.Course)
                    .Include(x => x.ClassSchedule)
                    .Include(x => x.Teacher)
                    .FirstOrDefaultAsync(x => x.Id == id);

                if (entity == null)
                {
                    return ApiResponse<LearningMaterialDto>.Fail("ERR_LEARNING_MATERIAL_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                return ApiResponse<LearningMaterialDto>.Ok(MapToDto(entity), "GET_LEARNING_MATERIAL_DETAIL_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<LearningMaterialDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<LearningMaterialDto>> CreateMaterialAsync(LearningMaterialSaveDto dto, string username)
        {
            try
            {
                var validationError = await ValidateAsync(dto, isEdit: false);
                if (validationError != null)
                {
                    return ApiResponse<LearningMaterialDto>.Fail(validationError, StatusCodes.Status400BadRequest);
                }

                var entity = dto.Adapt<LearningMaterial>();
                entity.Id = 0; // Đảm bảo auto-increment

                // Tự động gán người upload nếu người tạo là Giáo viên (Teacher)
                var teacher = await _dbContext.Teachers
                    .FirstOrDefaultAsync(t => t.Email == username || t.Code == username);
                if (teacher != null)
                {
                    entity.UploadedBy = teacher.Id;
                }
                else if (dto.UploadedBy.HasValue)
                {
                    entity.UploadedBy = dto.UploadedBy;
                }

                entity.Status = dto.Status != 0 ? dto.Status : 1;

                await _repository.AddAsync(entity);
                await _repository.SaveChangesAsync();

                // Load lại thông tin liên kết để trả về dữ liệu đầy đủ
                var createdEntity = await _repository.FindAll()
                    .Include(x => x.Class)
                    .Include(x => x.Course)
                    .Include(x => x.ClassSchedule)
                    .Include(x => x.Teacher)
                    .FirstOrDefaultAsync(x => x.Id == entity.Id);

                return ApiResponse<LearningMaterialDto>.Created(MapToDto(createdEntity!), "CREATE_LEARNING_MATERIAL_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<LearningMaterialDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<LearningMaterialDto>> EditMaterialAsync(
            LearningMaterialSaveDto dto, 
            string username, 
            IList<string> roles)
        {
            try
            {
                var validationError = await ValidateAsync(dto, isEdit: true);
                if (validationError != null)
                {
                    return ApiResponse<LearningMaterialDto>.Fail(validationError, StatusCodes.Status400BadRequest);
                }

                var entity = await _repository.GetByIdAsync(dto.Id);
                if (entity == null)
                {
                    return ApiResponse<LearningMaterialDto>.Fail("ERR_LEARNING_MATERIAL_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                // Kiểm tra phân quyền: Nếu không phải Admin/Academic Staff và là Teacher
                // Thì chỉ cho phép sửa tài liệu do chính mình tải lên
                var isAdminOrStaff = roles.Contains("Admin") || roles.Contains("AcademicStaff") || roles.Contains("Academic Staff");
                if (!isAdminOrStaff && roles.Contains("Teacher"))
                {
                    var teacher = await _dbContext.Teachers
                        .FirstOrDefaultAsync(t => t.Email == username || t.Code == username);

                    if (teacher == null || entity.UploadedBy != teacher.Id)
                    {
                        return ApiResponse<LearningMaterialDto>.Fail("ERR_FORBIDDEN_EDIT_OTHER_MATERIAL", StatusCodes.Status403Forbidden);
                    }
                }

                // Map DTO vào entity
                dto.Adapt(entity);

                // Giữ lại UploadedBy ban đầu nếu người sửa là Admin/Staff và không truyền UploadedBy mới
                if (isAdminOrStaff && !dto.UploadedBy.HasValue)
                {
                    // Giữ nguyên uploadedBy
                }
                else if (!isAdminOrStaff)
                {
                    // Nếu là giáo viên tự sửa thì gán lại ID của mình cho chắc chắn
                    var teacher = await _dbContext.Teachers
                        .FirstOrDefaultAsync(t => t.Email == username || t.Code == username);
                    if (teacher != null)
                    {
                        entity.UploadedBy = teacher.Id;
                    }
                }

                await _repository.UpdateAsync(entity);
                await _repository.SaveChangesAsync();

                var updatedEntity = await _repository.FindAll()
                    .Include(x => x.Class)
                    .Include(x => x.Course)
                    .Include(x => x.ClassSchedule)
                    .Include(x => x.Teacher)
                    .FirstOrDefaultAsync(x => x.Id == entity.Id);

                return ApiResponse<LearningMaterialDto>.Ok(MapToDto(updatedEntity!), "UPDATE_LEARNING_MATERIAL_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<LearningMaterialDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<bool>> DeleteMaterialAsync(int id, string username, IList<string> roles)
        {
            try
            {
                var entity = await _repository.GetByIdAsync(id);
                if (entity == null)
                {
                    return ApiResponse<bool>.Fail("ERR_LEARNING_MATERIAL_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                // Kiểm tra phân quyền sửa/xóa tài liệu của chính mình
                var isAdminOrStaff = roles.Contains("Admin") || roles.Contains("AcademicStaff") || roles.Contains("Academic Staff");
                if (!isAdminOrStaff && roles.Contains("Teacher"))
                {
                    var teacher = await _dbContext.Teachers
                        .FirstOrDefaultAsync(t => t.Email == username || t.Code == username);

                    if (teacher == null || entity.UploadedBy != teacher.Id)
                    {
                        return ApiResponse<bool>.Fail("ERR_FORBIDDEN_DELETE_OTHER_MATERIAL", StatusCodes.Status403Forbidden);
                    }
                }

                await _repository.DeleteAsync(entity);
                await _repository.SaveChangesAsync();
                return ApiResponse<bool>.Ok(true, "DELETE_LEARNING_MATERIAL_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<bool>> DeactiveMaterialAsync(int id, string username, IList<string> roles)
        {
            try
            {
                var entity = await _repository.GetByIdAsync(id);
                if (entity == null)
                {
                    return ApiResponse<bool>.Fail("ERR_LEARNING_MATERIAL_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                // Kiểm tra phân quyền sửa/xóa tài liệu của chính mình
                var isAdminOrStaff = roles.Contains("Admin") || roles.Contains("AcademicStaff") || roles.Contains("Academic Staff");
                if (!isAdminOrStaff && roles.Contains("Teacher"))
                {
                    var teacher = await _dbContext.Teachers
                        .FirstOrDefaultAsync(t => t.Email == username || t.Code == username);

                    if (teacher == null || entity.UploadedBy != teacher.Id)
                    {
                        return ApiResponse<bool>.Fail("ERR_FORBIDDEN_DELETE_OTHER_MATERIAL", StatusCodes.Status403Forbidden);
                    }
                }

                await _repository.DeactiveAsync(entity);
                await _repository.SaveChangesAsync();
                return ApiResponse<bool>.Ok(true, "DEACTIVATE_LEARNING_MATERIAL_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        // ===================== PRIVATE VALIDATE =====================
        private async Task<string?> ValidateAsync(LearningMaterialSaveDto dto, bool isEdit)
        {
            if (string.IsNullOrWhiteSpace(dto.Code)) return "ERR_CODE_EMPTY";
            if (dto.Code.Length > 50) return "ERR_CODE_MAX_LENGTH";
            if (string.IsNullOrWhiteSpace(dto.Name)) return "ERR_NAME_EMPTY";
            if (dto.Name.Length > 200) return "ERR_NAME_MAX_LENGTH";

            if (dto.Title != null && dto.Title.Length > 250) return "ERR_TITLE_MAX_LENGTH";
            if (dto.Description != null && dto.Description.Length > 1000) return "ERR_DESCRIPTION_MAX_LENGTH";
            if (dto.FileUrl != null && dto.FileUrl.Length > 500) return "ERR_FILE_URL_MAX_LENGTH";
            if (dto.FileType != null && dto.FileType.Length > 50) return "ERR_FILE_TYPE_MAX_LENGTH";

            // Kiểm tra trùng Code
            var duplicate = await _repository.FindAll()
                .FirstOrDefaultAsync(x => x.Code == dto.Code && (!isEdit || x.Id != dto.Id));
            if (duplicate != null) return "ERR_CODE_DUPLICATE";

            return null;
        }

        // ===================== PRIVATE MAPPING =====================
        private static LearningMaterialDto MapToDto(LearningMaterial entity) => new()
        {
            Id = entity.Id,
            Code = entity.Code ?? string.Empty,
            Name = entity.Name ?? string.Empty,
            ClassId = entity.ClassId,
            ClassName = entity.Class?.Name,
            ScheduleId = entity.ScheduleId,
            ScheduleName = entity.ClassSchedule != null ? $"Lesson {entity.ClassSchedule.LessonNo} ({entity.ClassSchedule.ScheduleDate:dd/MM/yyyy})" : null,
            UploadedBy = entity.UploadedBy,
            TeacherName = entity.Teacher?.Name,
            CourseId = entity.CourseId,
            CourseName = entity.Course?.Name,
            Title = entity.Title,
            Description = entity.Description,
            FileUrl = entity.FileUrl,
            FileType = entity.FileType,
            Status = entity.Status,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy
        };
    }
}

