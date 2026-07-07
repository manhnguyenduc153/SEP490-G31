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

namespace sep490_be.Services.Implementations
{
    public class ParentStudentService : IParentStudentService
    {
        private readonly IParentStudentRepository _repository;
        private readonly IStudentRepository _studentRepository;
        private readonly UserManager<IdentityUser> _userManager;

        private const string DefaultParentRole = "Parent";
        private const string DefaultParentPassword = "Parent@123456";

        public ParentStudentService(
            IParentStudentRepository repository,
            IStudentRepository studentRepository,
            UserManager<IdentityUser> userManager)
        {
            _repository = repository;
            _studentRepository = studentRepository;
            _userManager = userManager;
        }

        // ──────────────────────────────────────────────────────────────────────
        // GET ALL
        // ──────────────────────────────────────────────────────────────────────
        public async Task<ApiResponse<PagingResponse<ParentStudentDto>>> GetAllAsync(ParentStudentSearchDto searchDto)
        {
            try
            {
                IQueryable<ParentStudent> query = _repository.FindAll().Include(x => x.Student);

                // Filter theo học sinh
                if (searchDto.StudentId.HasValue && searchDto.StudentId.Value > 0)
                    query = query.Where(x => x.StudentId == searchDto.StudentId.Value);

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
                var dtos = entities.Select(MapToDto);
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
                    .Include(x => x.Student)
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
        // CREATE — tạo phụ huynh + tạo IdentityUser với role "Parent"
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

                // 2. Kiểm tra học sinh tồn tại
                var studentExists = await _studentRepository.ExistsAsync(s => s.Id == dto.StudentId);
                if (!studentExists)
                {
                    await _repository.RollbackTransactionAsync();
                    return ApiResponse<ParentStudentDto>.Fail("ERR_STUDENT_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                // 3. Tạo IdentityUser cho phụ huynh
                var identityUser = new IdentityUser
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

                // 5. Tạo ParentStudent entity
                var generatedCode = "PH" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var entity = new ParentStudent
                {
                    StudentId = dto.StudentId,
                    Code = generatedCode,
                    Name = dto.Name,            // Tên phụ huynh
                    TextSearch = StringHelper.GenerateTextSearch(generatedCode, dto.Name),
                    ParentPhone = dto.ParentPhone,
                    Email = dto.Email,
                    UserId = identityUser.Id,    // Liên kết với IdentityUser vừa tạo
                    Relationship = dto.Relationship,
                    Status = 1
                };

                await _repository.AddAsync(entity);
                await _repository.SaveChangesAsync();
                await _repository.CommitTransactionAsync();

                // Load navigation để trả về đủ thông tin
                var created = await _repository.FindByCondition(x => x.Id == entity.Id)
                    .Include(x => x.Student)
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
        // EDIT — cập nhật thông tin (không thay đổi email/account)
        // ──────────────────────────────────────────────────────────────────────
        public async Task<ApiResponse<ParentStudentDto>> EditAsync(ParentStudentSaveDto dto)
        {
            try
            {
                var entity = await _repository.FindByCondition(x => x.Id == dto.Id)
                    .Include(x => x.Student)
                    .FirstOrDefaultAsync();

                if (entity == null)
                    return ApiResponse<ParentStudentDto>.Fail("ERR_PARENT_NOT_FOUND", StatusCodes.Status404NotFound);

                // Validate dữ liệu khi edit
                var validationError = await ValidateEditAsync(dto);
                if (validationError != null)
                    return ApiResponse<ParentStudentDto>.Fail(validationError, StatusCodes.Status400BadRequest);

                // Cập nhật thông tin (không đổi Email, UserId và Code)
                entity.Name = dto.Name;
                entity.TextSearch = StringHelper.GenerateTextSearch(entity.Code, dto.Name);
                entity.ParentPhone = dto.ParentPhone;
                entity.Relationship = dto.Relationship;
                entity.Status = 1;

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

                await _repository.UpdateAsync(entity);
                await _repository.SaveChangesAsync();

                return ApiResponse<ParentStudentDto>.Ok(MapToDto(entity), "UPDATE_PARENT_SUCCESS");
            }
            catch (Exception ex)
            {
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
            StudentId = entity.StudentId,
            StudentName = entity.Student?.Name,
            ParentPhone = entity.ParentPhone,
            Email = entity.Email,
            Relationship = entity.Relationship,
            Status = entity.Status,
            UserId = entity.UserId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy
        };

        /// <summary>
        /// Validate khi tạo mới: Code chưa trùng, Email chưa tồn tại
        /// </summary>
        private async Task<string?> ValidateCreateAsync(ParentStudentSaveDto dto)
        {

            if (string.IsNullOrWhiteSpace(dto.Name))
                return "ERR_PARENT_NAME_EMPTY";

            if (string.IsNullOrWhiteSpace(dto.Email))
                return "ERR_EMAIL_EMPTY";

            if (dto.StudentId <= 0)
                return "ERR_STUDENT_ID_INVALID";

            if (!string.IsNullOrWhiteSpace(dto.ParentPhone))
            {
                var duplicatePhone = await _repository.FindAll()
                    .AnyAsync(x => x.ParentPhone == dto.ParentPhone);
                if (duplicatePhone)
                    return "ERR_PHONE_DUPLICATE";
            }

            // Kiểm tra email chưa có trong bảng parent_students (với cùng student)
            var duplicateInParent = await _repository.FindAll()
                .AnyAsync(x => x.Email == dto.Email && x.StudentId == dto.StudentId);
            if (duplicateInParent)
                return "ERR_PARENT_EMAIL_ALREADY_LINKED_TO_STUDENT";

            // Kiểm tra email chưa có trong IdentityUsers
            var existingUser = await _userManager.FindByEmailAsync(dto.Email);
            if (existingUser != null)
                return "ERR_EMAIL_ALREADY_EXISTS_IN_USERS";

            return null;
        }

        /// <summary>
        /// Validate khi chỉnh sửa: Code/Name không rỗng, Code không trùng record khác
        /// </summary>
        private async Task<string?> ValidateEditAsync(ParentStudentSaveDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return "ERR_PARENT_NAME_EMPTY";

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

