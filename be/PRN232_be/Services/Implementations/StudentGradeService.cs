using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PRN232_be.DTO;
using PRN232_be.DTO.StudentGrade;
using PRN232_be.Models;
using PRN232_be.Services.Interfaces;

namespace PRN232_be.Services.Implementations
{
    public class StudentGradeService : IStudentGradeService
    {
        private readonly ApplicationDbContext _dbContext;

        public StudentGradeService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<ApiResponse<ClassGradeSettingsDto>> GetSettingsAsync(int classId)
        {
            try
            {
                var classInfo = await _dbContext.Classes
                    .Where(c => c.Id == classId && !c.IsDeleted)
                    .Select(c => new { c.Id, c.CourseId })
                    .FirstOrDefaultAsync();

                if (classInfo == null)
                {
                    return ApiResponse<ClassGradeSettingsDto>.Fail("ERR_CLASS_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                if (!classInfo.CourseId.HasValue)
                {
                    return ApiResponse<ClassGradeSettingsDto>.Fail("ERR_CLASS_COURSE_NOT_FOUND", StatusCodes.Status400BadRequest);
                }

                var courseId = classInfo.CourseId.Value;
                await EnsureDefaultComponentsAsync(courseId);

                var components = await _dbContext.GradeComponents
                    .Where(x => x.CourseId == courseId)
                    .OrderBy(x => x.SortOrder)
                    .ThenBy(x => x.Id)
                    .ToListAsync();

                var studentClassIds = await _dbContext.StudentClasses
                    .Where(sc => sc.ClassId == classId)
                    .Select(sc => sc.Id)
                    .ToListAsync();

                var componentIds = components.Select(x => x.Id).ToHashSet();
                var overrides = await _dbContext.StudentGradeOverrides
                    .Include(x => x.StudentClass)
                    .Include(x => x.GradeComponent)
                    .Where(x => studentClassIds.Contains(x.StudentClassId) && componentIds.Contains(x.GradeComponentId))
                    .OrderBy(x => x.StudentClassId)
                    .ThenBy(x => x.GradeComponent.SortOrder)
                    .Select(x => new StudentGradeOverrideDto
                    {
                        Id = x.Id,
                        StudentClassId = x.StudentClassId,
                        StudentId = x.StudentClass.StudentId,
                        GradeComponentId = x.GradeComponentId,
                        ComponentCode = x.GradeComponent.Code,
                        Score = x.Score
                    })
                    .ToListAsync();

                return ApiResponse<ClassGradeSettingsDto>.Ok(new ClassGradeSettingsDto
                {
                    ClassId = classId,
                    CourseId = courseId,
                    Components = components.Select(MapComponent).ToList(),
                    Overrides = overrides
                }, "GET_GRADE_SETTINGS_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<ClassGradeSettingsDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<List<GradeComponentDto>>> GetCourseComponentsAsync(int courseId)
        {
            try
            {
                var exists = await _dbContext.Courses.AnyAsync(c => c.Id == courseId && !c.IsDeleted);
                if (!exists)
                {
                    return ApiResponse<List<GradeComponentDto>>.Fail("ERR_COURSE_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                await EnsureDefaultComponentsAsync(courseId);

                var result = await _dbContext.GradeComponents
                    .Where(x => x.CourseId == courseId)
                    .OrderBy(x => x.SortOrder)
                    .ThenBy(x => x.Id)
                    .Select(x => MapComponent(x))
                    .ToListAsync();

                return ApiResponse<List<GradeComponentDto>>.Ok(result, "GET_GRADE_COMPONENTS_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<List<GradeComponentDto>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<List<GradeComponentDto>>> SaveCourseComponentsAsync(int courseId, ClassGradeComponentsSaveDto dto)
        {
            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                var exists = await _dbContext.Courses.AnyAsync(c => c.Id == courseId && !c.IsDeleted);
                if (!exists)
                {
                    return ApiResponse<List<GradeComponentDto>>.Fail("ERR_COURSE_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                if (dto.Components.Count == 0)
                {
                    return ApiResponse<List<GradeComponentDto>>.Fail("ERR_GRADE_COMPONENT_EMPTY", StatusCodes.Status400BadRequest);
                }

                var existing = await _dbContext.GradeComponents
                    .Where(x => x.CourseId == courseId)
                    .ToListAsync();

                var keepIds = dto.Components.Where(x => x.Id.HasValue && x.Id.Value > 0).Select(x => x.Id!.Value).ToHashSet();
                var removed = existing.Where(x => !keepIds.Contains(x.Id)).ToList();
                if (removed.Count > 0)
                {
                    _dbContext.GradeComponents.RemoveRange(removed);
                }

                for (var index = 0; index < dto.Components.Count; index++)
                {
                    var item = dto.Components[index];
                    if (string.IsNullOrWhiteSpace(item.Name))
                    {
                        return ApiResponse<List<GradeComponentDto>>.Fail("ERR_GRADE_COMPONENT_NAME_EMPTY", StatusCodes.Status400BadRequest);
                    }

                    var code = string.IsNullOrWhiteSpace(item.Code)
                        ? $"custom_{Guid.NewGuid():N}"
                        : item.Code.Trim();

                    var entity = item.Id.HasValue && item.Id.Value > 0
                        ? existing.FirstOrDefault(x => x.Id == item.Id.Value)
                        : null;

                    if (entity == null)
                    {
                        entity = new GradeComponent
                        {
                            CourseId = courseId,
                            Code = code
                        };
                        _dbContext.GradeComponents.Add(entity);
                    }

                    entity.Name = item.Name.Trim();
                    entity.Weight = item.Weight < 0 ? 0 : item.Weight;
                    entity.SortOrder = item.SortOrder > 0 ? item.SortOrder : index + 1;
                    entity.IsSystem = item.IsSystem;
                }

                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                var result = await _dbContext.GradeComponents
                    .Where(x => x.CourseId == courseId)
                    .OrderBy(x => x.SortOrder)
                    .ThenBy(x => x.Id)
                    .Select(x => MapComponent(x))
                    .ToListAsync();

                return ApiResponse<List<GradeComponentDto>>.Ok(result, "SAVE_GRADE_COMPONENTS_SUCCESS");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return ApiResponse<List<GradeComponentDto>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<List<StudentGradeOverrideDto>>> SaveOverridesAsync(int classId, StudentGradeOverridesSaveDto dto)
        {
            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                var courseId = await _dbContext.Classes
                    .Where(c => c.Id == classId && !c.IsDeleted)
                    .Select(c => c.CourseId)
                    .FirstOrDefaultAsync();

                if (!courseId.HasValue)
                {
                    return ApiResponse<List<StudentGradeOverrideDto>>.Fail("ERR_CLASS_COURSE_NOT_FOUND", StatusCodes.Status400BadRequest);
                }

                var studentClassIds = await _dbContext.StudentClasses
                    .Where(sc => sc.ClassId == classId)
                    .Select(sc => sc.Id)
                    .ToListAsync();
                var componentIds = await _dbContext.GradeComponents
                    .Where(gc => gc.CourseId == courseId.Value)
                    .Select(gc => gc.Id)
                    .ToListAsync();

                var existing = await _dbContext.StudentGradeOverrides
                    .IgnoreQueryFilters()
                    .Where(x => studentClassIds.Contains(x.StudentClassId) && componentIds.Contains(x.GradeComponentId))
                    .ToListAsync();
                var existingByKey = existing.ToDictionary(
                    x => (x.StudentClassId, x.GradeComponentId),
                    x => x);

                foreach (var item in dto.Overrides)
                {
                    if (!studentClassIds.Contains(item.StudentClassId) || !componentIds.Contains(item.GradeComponentId))
                    {
                        continue;
                    }

                    existingByKey.TryGetValue((item.StudentClassId, item.GradeComponentId), out var entity);

                    if (!item.Score.HasValue)
                    {
                        if (entity != null && !entity.IsDeleted)
                        {
                            _dbContext.StudentGradeOverrides.Remove(entity);
                        }
                        continue;
                    }

                    var score = Math.Max(0, Math.Min(10, item.Score.Value));
                    if (entity == null)
                    {
                        entity = new StudentGradeOverride
                        {
                            StudentClassId = item.StudentClassId,
                            GradeComponentId = item.GradeComponentId
                        };
                        _dbContext.StudentGradeOverrides.Add(entity);
                        existingByKey[(item.StudentClassId, item.GradeComponentId)] = entity;
                    }

                    entity.IsDeleted = false;
                    entity.DeletedAt = null;
                    entity.DeletedBy = null;
                    entity.Score = score;
                }

                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                var result = await _dbContext.StudentGradeOverrides
                    .Include(x => x.StudentClass)
                    .Include(x => x.GradeComponent)
                    .Where(x => studentClassIds.Contains(x.StudentClassId) && componentIds.Contains(x.GradeComponentId))
                    .OrderBy(x => x.StudentClassId)
                    .ThenBy(x => x.GradeComponent.SortOrder)
                    .Select(x => new StudentGradeOverrideDto
                    {
                        Id = x.Id,
                        StudentClassId = x.StudentClassId,
                        StudentId = x.StudentClass.StudentId,
                        GradeComponentId = x.GradeComponentId,
                        ComponentCode = x.GradeComponent.Code,
                        Score = x.Score
                    })
                    .ToListAsync();

                return ApiResponse<List<StudentGradeOverrideDto>>.Ok(result, "SAVE_GRADE_OVERRIDES_SUCCESS");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return ApiResponse<List<StudentGradeOverrideDto>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        private async Task EnsureDefaultComponentsAsync(int courseId)
        {
            var hasComponents = await _dbContext.GradeComponents.AnyAsync(x => x.CourseId == courseId);
            if (hasComponents)
            {
                return;
            }

            _dbContext.GradeComponents.AddRange(
                new GradeComponent { CourseId = courseId, Code = "attendance", Name = "Attendance", Weight = 30, SortOrder = 1, IsSystem = true },
                new GradeComponent { CourseId = courseId, Code = "homework", Name = "Homework", Weight = 30, SortOrder = 2, IsSystem = true },
                new GradeComponent { CourseId = courseId, Code = "exam", Name = "Exam", Weight = 40, SortOrder = 3, IsSystem = true }
            );
            await _dbContext.SaveChangesAsync();
        }

        private static GradeComponentDto MapComponent(GradeComponent entity) => new()
        {
            Id = entity.Id,
            CourseId = entity.CourseId,
            Code = entity.Code,
            Name = entity.Name,
            Weight = entity.Weight,
            SortOrder = entity.SortOrder,
            IsSystem = entity.IsSystem
        };
    }
}
