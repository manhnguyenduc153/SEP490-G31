using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using sep490_be.DTO;
using sep490_be.DTO.Common;
using sep490_be.DTO.ParentStudent;
using sep490_be.Helpers;
using sep490_be.Models;
using sep490_be.Repositories.Interfaces;
using sep490_be.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace sep490_be.Services.Implementations
{
    public class ParentStudentService : IParentStudentService
    {
        private readonly IParentStudentRepository _repository;
        private readonly IStudentRepository _studentRepository;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ApplicationDbContext _dbContext;

        private const string DefaultParentRole = "Parent";
        private const string DefaultParentPassword = "123456";

        public ParentStudentService(
            IParentStudentRepository repository,
            IStudentRepository studentRepository,
            UserManager<IdentityUser> userManager,
            ApplicationDbContext dbContext)
        {
            _repository = repository;
            _studentRepository = studentRepository;
            _userManager = userManager;
            _dbContext = dbContext;
        }

        // ──────────────────────────────────────────────────────────────────────
        // GET ALL
        // ──────────────────────────────────────────────────────────────────────
        public async Task<ApiResponse<PagingResponse<ParentStudentDto>>> GetAllAsync(ParentStudentSearchDto searchDto)
        {
            try
            {
                IQueryable<ParentStudent> query = _repository.FindAll()
                    .AsNoTracking()
                    .Include(x => x.ParentStudentLinks)
                        .ThenInclude(l => l.Student)
                    .Distinct();

                // Filter theo học sinh
                if (searchDto.StudentId.HasValue && searchDto.StudentId.Value > 0)
                {
                    query = query.Where(x => x.ParentStudentLinks.Any(l => l.StudentId == searchDto.StudentId.Value));
                }

                // Tìm kiếm theo TextSearch (Code + Name) hoặc phone, email
                if (!string.IsNullOrWhiteSpace(searchDto.Keyword))
                {
                    var kw = searchDto.Keyword.ToLower();
                    query = query.Where(x =>
                        (x.TextSearch != null && x.TextSearch.Contains(kw)) ||
                        (x.ParentPhone != null && x.ParentPhone.Contains(kw)) ||
                        (x.Email != null && x.Email.ToLower().Contains(kw)));
                }

                var totalRecords = await query.CountAsync();
                var entities = await query.ApplyPagingAsync(searchDto);
                var dtos = entities.Select(MapToDto).ToList();
                var pagingResponse = dtos.ToPagingResponse(totalRecords, searchDto);

                return ApiResponse<PagingResponse<ParentStudentDto>>.Ok(pagingResponse, "GET_PARENT_LIST_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<PagingResponse<ParentStudentDto>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // GET BY ID
        // ──────────────────────────────────────────────────────────────────────
        public async Task<ApiResponse<ParentStudentDto>> GetByIdAsync(int id)
        {
            try
            {
                var entity = await _repository.FindByCondition(x => x.Id == id)
                    .Include(x => x.ParentStudentLinks)
                        .ThenInclude(l => l.Student)
                    .FirstOrDefaultAsync();

                if (entity == null)
                    return ApiResponse<ParentStudentDto>.Fail("ERR_PARENT_NOT_FOUND", StatusCodes.Status404NotFound);

                return ApiResponse<ParentStudentDto>.Ok(MapToDto(entity), "GET_PARENT_DETAIL_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<ParentStudentDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // CREATE — tạo phụ huynh + tạo IdentityUser + tạo liên kết học sinh
        // ──────────────────────────────────────────────────────────────────────
        public async Task<ApiResponse<ParentStudentDto>> CreateAsync(ParentStudentSaveDto dto)
        {
            var transaction = await _repository.BeginTransactionAsync();
            try
            {
                // 1. Validate dữ liệu đầu vào
                var validationError = await ValidateCreateAsync(dto);
                if (validationError != null)
                {
                    await _repository.RollbackTransactionAsync();
                    return ApiResponse<ParentStudentDto>.Fail(validationError, StatusCodes.Status400BadRequest);
                }

                // 2. Kiểm tra các học sinh được liên kết có tồn tại hay không
                if (dto.StudentIds != null && dto.StudentIds.Any())
                {
                    foreach (var studentId in dto.StudentIds)
                    {
                        var studentExists = await _studentRepository.ExistsAsync(s => s.Id == studentId);
                        if (!studentExists)
                        {
                            await _repository.RollbackTransactionAsync();
                            return ApiResponse<ParentStudentDto>.Fail("ERR_STUDENT_NOT_FOUND", StatusCodes.Status404NotFound);
                        }
                    }
                }

                // 3. Kiểm tra xem IdentityUser đã tồn tại chưa
                var identityUser = await _userManager.FindByEmailAsync(dto.Email);
                if (identityUser == null)
                {
                    // Tạo mới IdentityUser cho phụ huynh
                    identityUser = new IdentityUser
                    {
                        UserName = dto.Email,
                        Email = dto.Email,
                        PhoneNumber = dto.ParentPhone,
                        SecurityStamp = Guid.NewGuid().ToString(),
                        LockoutEnabled = true
                    };

                    var createResult = await _userManager.CreateAsync(identityUser, DefaultParentPassword);
                    if (!createResult.Succeeded)
                    {
                        await _repository.RollbackTransactionAsync();
                        var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                        return ApiResponse<ParentStudentDto>.Fail($"ERR_CREATE_USER_FAILED: {errors}", StatusCodes.Status400BadRequest);
                    }

                    // 4. Gán role "Parent" cho IdentityUser
                    var roleResult = await _userManager.AddToRoleAsync(identityUser, DefaultParentRole);
                    if (!roleResult.Succeeded)
                    {
                        await _userManager.DeleteAsync(identityUser);
                        await _repository.RollbackTransactionAsync();
                        var errors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
                        return ApiResponse<ParentStudentDto>.Fail($"ERR_ASSIGN_ROLE_FAILED: {errors}", StatusCodes.Status400BadRequest);
                    }
                }

                // 5. Tạo ParentStudent entity
                var generatedCode = "PH" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var entity = new ParentStudent
                {
                    Code = generatedCode,
                    Name = dto.Name,            // Tên phụ huynh
                    TextSearch = StringHelper.GenerateTextSearch(generatedCode, dto.Name),
                    ParentPhone = dto.ParentPhone,
                    Email = dto.Email,
                    UserId = identityUser.Id,    // Liên kết với IdentityUser
                    Status = 1
                };

                await _repository.AddAsync(entity);
                await _repository.SaveChangesAsync();

                // 6. Tạo các liên kết với học sinh (sử dụng mối quan hệ chung)
                if (dto.StudentIds != null && dto.StudentIds.Any())
                {
                    foreach (var studentId in dto.StudentIds)
                    {
                        var link = new ParentStudentLink
                        {
                            ParentId = entity.Id,
                            StudentId = studentId,
                            Relationship = dto.Relationship,
                            Status = 1
                        };
                        await _dbContext.ParentStudentLinks.AddAsync(link);
                    }
                    await _dbContext.SaveChangesAsync();
                }

                await _repository.CommitTransactionAsync();

                // Load navigation để trả về đủ thông tin
                var created = await _repository.FindByCondition(x => x.Id == entity.Id)
                    .AsNoTracking()
                    .Include(x => x.ParentStudentLinks)
                        .ThenInclude(l => l.Student)
                    .FirstOrDefaultAsync();

                return ApiResponse<ParentStudentDto>.Created(MapToDto(created!), "CREATE_PARENT_SUCCESS");
            }
            catch (Exception ex)
            {
                await _repository.RollbackTransactionAsync();
                return ApiResponse<ParentStudentDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // EDIT — cập nhật thông tin
        // ──────────────────────────────────────────────────────────────────────
        public async Task<ApiResponse<ParentStudentDto>> EditAsync(ParentStudentSaveDto dto)
        {
            var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                var entity = await _repository.FindByCondition(x => x.Id == dto.Id, trackChanges: true)
                    .FirstOrDefaultAsync();

                if (entity == null)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<ParentStudentDto>.Fail("ERR_PARENT_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                // Validate dữ liệu khi edit
                var validationError = await ValidateEditAsync(dto);
                if (validationError != null)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<ParentStudentDto>.Fail(validationError, StatusCodes.Status400BadRequest);
                }

                // Cập nhật thông tin (không đổi Email, UserId và Code)
                entity.Name = dto.Name;
                entity.TextSearch = StringHelper.GenerateTextSearch(entity.Code, dto.Name);
                entity.ParentPhone = dto.ParentPhone;

                // Đồng bộ số điện thoại lên IdentityUser nếu có liên kết
                if (!string.IsNullOrEmpty(entity.UserId))
                {
                    var identityUser = await _userManager.FindByIdAsync(entity.UserId);
                    if (identityUser != null)
                    {
                        identityUser.PhoneNumber = dto.ParentPhone;
                        await _userManager.UpdateAsync(identityUser);
                    }
                }

                // Cập nhật các liên kết với học sinh
                var existingLinks = await _dbContext.ParentStudentLinks
                    .Where(l => l.ParentId == entity.Id)
                    .ToListAsync();

                // Xóa các liên kết không còn trong DTO gửi lên
                var newStudentIds = dto.StudentIds ?? new List<int>();
                var linksToRemove = existingLinks.Where(l => !newStudentIds.Contains(l.StudentId)).ToList();
                _dbContext.ParentStudentLinks.RemoveRange(linksToRemove);

                // Thêm mới hoặc cập nhật các liên kết hiện tại
                foreach (var studentId in newStudentIds)
                {
                    var existingLink = existingLinks.FirstOrDefault(l => l.StudentId == studentId);
                    if (existingLink != null)
                    {
                        existingLink.Relationship = dto.Relationship;
                    }
                    else
                    {
                        var newLink = new ParentStudentLink
                        {
                            ParentId = entity.Id,
                            StudentId = studentId,
                            Relationship = dto.Relationship,
                            Status = 1
                        };
                        await _dbContext.ParentStudentLinks.AddAsync(newLink);
                    }
                }

                await _repository.SaveChangesAsync();
                await transaction.CommitAsync();

                // Load lại đầy đủ DTO
                var updated = await _repository.FindByCondition(x => x.Id == entity.Id)
                    .AsNoTracking()
                    .Include(x => x.ParentStudentLinks)
                        .ThenInclude(l => l.Student)
                    .FirstOrDefaultAsync();

                return ApiResponse<ParentStudentDto>.Ok(MapToDto(updated!), "UPDATE_PARENT_SUCCESS");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return ApiResponse<ParentStudentDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // DELETE — soft-delete ParentStudent + lock IdentityUser
        // ──────────────────────────────────────────────────────────────────────
        public async Task<ApiResponse<bool>> DeleteAsync(int id)
        {
            try
            {
                var entity = await _repository.GetByIdAsync(id);
                if (entity == null)
                    return ApiResponse<bool>.Fail("ERR_PARENT_NOT_FOUND", StatusCodes.Status404NotFound);

                // Lock IdentityUser để phụ huynh không đăng nhập được nữa
                if (!string.IsNullOrEmpty(entity.UserId))
                {
                    var identityUser = await _userManager.FindByIdAsync(entity.UserId);
                    if (identityUser != null)
                    {
                        identityUser.LockoutEnd = DateTimeOffset.MaxValue;  // Lock vĩnh viễn
                        await _userManager.UpdateAsync(identityUser);
                    }
                }

                await _repository.DeleteAsync(entity);   // → intercepted thành soft-delete
                await _repository.SaveChangesAsync();

                return ApiResponse<bool>.Ok(true, "DELETE_PARENT_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // DEACTIVE
        // ──────────────────────────────────────────────────────────────────────
        public async Task<ApiResponse<bool>> DeactiveAsync(int id)
        {
            try
            {
                var entity = await _repository.GetByIdAsync(id);
                if (entity == null)
                    return ApiResponse<bool>.Fail("ERR_PARENT_NOT_FOUND", StatusCodes.Status404NotFound);

                await _repository.DeactiveAsync(entity);
                await _repository.SaveChangesAsync();

                return ApiResponse<bool>.Ok(true, "DEACTIVATE_PARENT_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // PRIVATE HELPERS
        // ──────────────────────────────────────────────────────────────────────

        private static ParentStudentDto MapToDto(ParentStudent entity) => new()
        {
            Id = entity.Id,
            Code = entity.Code,
            Name = entity.Name,             // Tên phụ huynh
            ParentPhone = entity.ParentPhone,
            Email = entity.Email,
            Status = entity.Status,
            UserId = entity.UserId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            Relationship = entity.ParentStudentLinks?.FirstOrDefault(l => !string.IsNullOrEmpty(l.Relationship))?.Relationship 
                           ?? entity.ParentStudentLinks?.FirstOrDefault()?.Relationship,
            Children = entity.ParentStudentLinks?.Select(l => new ChildDto
            {
                StudentId = l.StudentId,
                StudentName = l.Student?.Name,
                Relationship = l.Relationship
            }).ToList() ?? new List<ChildDto>()
        };

        private async Task<string?> ValidateCreateAsync(ParentStudentSaveDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return "ERR_PARENT_NAME_EMPTY";

            if (dto.Name.Trim().Length < 5)
                return "ERR_NAME_MIN_LENGTH";

            if (string.IsNullOrWhiteSpace(dto.Email))
                return "ERR_EMAIL_EMPTY";

            if (!string.IsNullOrWhiteSpace(dto.ParentPhone))
            {
                var duplicatePhone = await _repository.FindAll()
                    .AnyAsync(x => x.ParentPhone == dto.ParentPhone && x.Email != dto.Email);
                if (duplicatePhone)
                    return "ERR_PHONE_DUPLICATE";
            }

            // Kiểm tra email trong IdentityUsers nếu đã tồn tại nhưng có vai trò khác
            var existingUser = await _userManager.FindByEmailAsync(dto.Email);
            if (existingUser != null)
            {
                var roles = await _userManager.GetRolesAsync(existingUser);
                if (!roles.Contains(DefaultParentRole))
                {
                    return "ERR_EMAIL_ALREADY_EXISTS_WITH_DIFFERENT_ROLE";
                }
            }

            return null;
        }

        /// <summary>
        /// Validate khi chỉnh sửa: Code/Name không rỗng, Code không trùng record khác
        /// </summary>
        private async Task<string?> ValidateEditAsync(ParentStudentSaveDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return "ERR_PARENT_NAME_EMPTY";

            if (dto.Name.Trim().Length < 5)
                return "ERR_NAME_MIN_LENGTH";

            if (!string.IsNullOrWhiteSpace(dto.ParentPhone))
            {
                var duplicatePhone = await _repository.FindAll()
                    .AnyAsync(x => x.ParentPhone == dto.ParentPhone && x.Id != dto.Id);
                if (duplicatePhone)
                    return "ERR_PHONE_DUPLICATE";
            }

            return null;
        }
    }
}
