using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using sep490_be.DTO;
using sep490_be.DTO.StudentGrade;
using sep490_be.Models;
using sep490_be.Services.Interfaces;
using sep490_be.Repositories.Interfaces;

namespace sep490_be.Services.Implementations
{
    public class StudentGradeService : IStudentGradeService
    {
        private readonly IStudentGradeRepository _repository;
        private readonly UserManager<IdentityUser> _userManager;

        public StudentGradeService(IStudentGradeRepository repository, UserManager<IdentityUser> userManager)
        {
            _repository = repository;
            _userManager = userManager;
        }

        public async Task<ApiResponse<ClassGradeSettingsDto>> GetSettingsAsync(int classId)
        {
            try
            {
                var classInfo = await _repository.GetClassInfoAsync(classId);

                if (classInfo == null)
                {
                    return ApiResponse<ClassGradeSettingsDto>.Fail("ERR_CLASS_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                if (!classInfo.Value.CourseId.HasValue)
                {
                    return ApiResponse<ClassGradeSettingsDto>.Fail("ERR_CLASS_COURSE_NOT_FOUND", StatusCodes.Status400BadRequest);
                }

                var courseId = classInfo.Value.CourseId.Value;
                await EnsureDefaultComponentsAsync(courseId);

                var components = await _repository.GetComponentsAsync(courseId);
                var studentClassIds = await _repository.GetStudentClassIdsAsync(classId);

                var componentIds = components.Select(x => x.Id).ToList();
                var overrides = await _repository.GetOverridesAsync(studentClassIds, componentIds);

                var overrideDtos = overrides.Select(x => new StudentGradeOverrideDto
                    {
                        Id = x.Id,
                        StudentClassId = x.StudentClassId,
                        StudentId = x.StudentClass.StudentId,
                        GradeComponentId = x.GradeComponentId,
                        ComponentCode = x.GradeComponent.Code,
                        Score = x.Score
                    })
                    .ToList();

                return ApiResponse<ClassGradeSettingsDto>.Ok(new ClassGradeSettingsDto
                {
                    ClassId = classId,
                    CourseId = courseId,
                    Components = components.Select(MapComponent).ToList(),
                    Overrides = overrideDtos
                }, "GET_GRADE_SETTINGS_SUCCESS");
            }
            catch (Exception)
            {
                return ApiResponse<ClassGradeSettingsDto>.Fail("ERR_INTERNAL_SERVER_ERROR", StatusCodes.Status500InternalServerError);
            }
        }

        private async Task<List<MyGradeClassDto>> GetGradesForStudentAsync(int studentId)
        {
            var enrollments = await _repository.GetStudentEnrollmentsAsync(studentId);

            var result = new List<MyGradeClassDto>();

            foreach (var enrollment in enrollments)
            {
                var classInfo = enrollment.Class;
                if (!classInfo.CourseId.HasValue)
                {
                    continue;
                }

                var courseId = classInfo.CourseId.Value;
                await EnsureDefaultComponentsAsync(courseId);

                var components = await _repository.GetComponentsAsync(courseId);
                var componentIds = components.Select(x => x.Id).ToList();
                var overrides = await _repository.GetStudentOverridesAsync(enrollment.Id, componentIds);

                var rawScores = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
                {
                    ["homework"] = await _repository.CalculateHomeworkScoreAsync(classInfo.Id, studentId)
                };

                var scoreComponents = components.Select(component =>
                {
                    var rawScore = rawScores.TryGetValue(component.Code, out var value) ? value : 0m;
                    var hasOverride = overrides.TryGetValue(component.Id, out var overrideScore);
                    return new MyGradeComponentScoreDto
                    {
                        GradeComponentId = component.Id,
                        ComponentCode = component.Code,
                        ComponentName = component.Name,
                        Weight = component.Weight,
                        RawScore = Round1(rawScore),
                        Score = Round1(hasOverride && overrideScore.HasValue ? overrideScore.Value : rawScore),
                        IsOverride = hasOverride
                    };
                }).ToList();

                var totalWeight = scoreComponents.Sum(x => Math.Max(0m, x.Weight));
                var average = totalWeight > 0
                    ? scoreComponents.Sum(x => x.Score * Math.Max(0m, x.Weight)) / totalWeight
                    : 0m;

                result.Add(new MyGradeClassDto
                {
                    ClassId = classInfo.Id,
                    ClassCode = classInfo.Code,
                    ClassName = classInfo.Name,
                    CourseId = classInfo.CourseId,
                    CourseCode = classInfo.Course?.Code,
                    CourseName = classInfo.Course?.Name,
                    AverageScore = Round1(average),
                    Components = scoreComponents
                });
            }

            return result;
        }

        public async Task<ApiResponse<List<MyGradeClassDto>>> GetMyGradesAsync(IEnumerable<string> identifiers)
        {
            try
            {
                var lookup = identifiers
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Distinct()
                    .ToList();
                var lookupSet = lookup.ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (lookup.Count == 0)
                {
                    return ApiResponse<List<MyGradeClassDto>>.Fail("ERR_STUDENT_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                var student = await _repository.ResolveStudentByIdentifiersAsync(lookup, lookupSet);

                if (student == null)
                {
                    return ApiResponse<List<MyGradeClassDto>>.Fail("ERR_STUDENT_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                var result = await GetGradesForStudentAsync(student.Id);
                return ApiResponse<List<MyGradeClassDto>>.Ok(result, "GET_MY_GRADES_SUCCESS");
            }
            catch (Exception)
            {
                return ApiResponse<List<MyGradeClassDto>>.Fail("ERR_INTERNAL_SERVER_ERROR", StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<List<MyGradeClassDto>>> GetChildGradesAsync(string username, int studentId)
        {
            try
            {
                var user = await _userManager.FindByNameAsync(username);
                if (user == null)
                {
                    return ApiResponse<List<MyGradeClassDto>>.Fail("ERR_USER_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                // Kiểm tra quyền: phụ huynh phải được liên kết với học sinh này, hoặc là admin/giáo viên
                var roles = await _userManager.GetRolesAsync(user);
                var isAdminOrTeacher = roles.Contains("Admin") || roles.Contains("Teacher");
                if (!isAdminOrTeacher)
                {
                    var isParentOfStudent = await _repository.IsParentOfStudentAsync(user.Email, studentId);
                    if (!isParentOfStudent)
                    {
                        return ApiResponse<List<MyGradeClassDto>>.Fail("ERR_UNAUTHORIZED", StatusCodes.Status403Forbidden);
                    }
                }

                var studentExists = await _repository.StudentExistsAsync(studentId);
                if (!studentExists)
                {
                    return ApiResponse<List<MyGradeClassDto>>.Fail("ERR_STUDENT_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                var result = await GetGradesForStudentAsync(studentId);
                return ApiResponse<List<MyGradeClassDto>>.Ok(result, "GET_CHILD_GRADES_SUCCESS");
            }
            catch (Exception)
            {
                return ApiResponse<List<MyGradeClassDto>>.Fail("ERR_INTERNAL_SERVER_ERROR", StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<List<GradeComponentDto>>> GetCourseComponentsAsync(int courseId)
        {
            try
            {
                var exists = await _repository.CourseExistsAsync(courseId);
                if (!exists)
                {
                    return ApiResponse<List<GradeComponentDto>>.Fail("ERR_COURSE_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                await EnsureDefaultComponentsAsync(courseId);

                var components = await _repository.GetComponentsAsync(courseId);
                var result = components.Select(x => MapComponent(x)).ToList();

                return ApiResponse<List<GradeComponentDto>>.Ok(result, "GET_GRADE_COMPONENTS_SUCCESS");
            }
            catch (Exception)
            {
                return ApiResponse<List<GradeComponentDto>>.Fail("ERR_INTERNAL_SERVER_ERROR", StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<List<GradeComponentDto>>> SaveCourseComponentsAsync(int courseId, ClassGradeComponentsSaveDto dto)
        {
            try
            {
                if (courseId <= 0)
                {
                    return ApiResponse<List<GradeComponentDto>>.Fail("ERR_COURSE_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                var exists = await _repository.CourseExistsAsync(courseId);
                if (!exists)
                {
                    return ApiResponse<List<GradeComponentDto>>.Fail("ERR_COURSE_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                if (dto?.Components == null || dto.Components.Count == 0)
                {
                    return ApiResponse<List<GradeComponentDto>>.Fail("ERR_GRADE_COMPONENT_EMPTY", StatusCodes.Status400BadRequest);
                }

                var existing = await _repository.GetExistingComponentsAsync(courseId);

                var suppliedIds = dto.Components
                    .Where(x => x.Id.HasValue && x.Id.Value > 0)
                    .Select(x => x.Id!.Value)
                    .ToList();
                if (suppliedIds.Count != suppliedIds.Distinct().Count())
                {
                    return ApiResponse<List<GradeComponentDto>>.Fail("ERR_GRADE_COMPONENT_DUPLICATE", StatusCodes.Status400BadRequest);
                }

                if (suppliedIds.Any(id => existing.All(x => x.Id != id)))
                {
                    return ApiResponse<List<GradeComponentDto>>.Fail("ERR_GRADE_COMPONENT_INVALID", StatusCodes.Status400BadRequest);
                }

                var candidateCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in dto.Components)
                {
                    if (string.IsNullOrWhiteSpace(item.Name))
                    {
                        return ApiResponse<List<GradeComponentDto>>.Fail("ERR_GRADE_COMPONENT_NAME_EMPTY", StatusCodes.Status400BadRequest);
                    }

                    if (item.Name.Trim().Length > 200)
                    {
                        return ApiResponse<List<GradeComponentDto>>.Fail("ERR_GRADE_COMPONENT_NAME_MAX_LENGTH", StatusCodes.Status400BadRequest);
                    }

                    if (item.Weight < 0 || item.Weight > 100)
                    {
                        return ApiResponse<List<GradeComponentDto>>.Fail("ERR_GRADE_COMPONENT_WEIGHT_RANGE", StatusCodes.Status400BadRequest);
                    }

                    var current = item.Id.HasValue ? existing.FirstOrDefault(x => x.Id == item.Id.Value) : null;
                    var code = current?.IsSystem == true
                        ? current.Code
                        : item.Code?.Trim();
                    if (!string.IsNullOrWhiteSpace(code))
                    {
                        if (code.Length > 100)
                        {
                            return ApiResponse<List<GradeComponentDto>>.Fail("ERR_GRADE_COMPONENT_CODE_MAX_LENGTH", StatusCodes.Status400BadRequest);
                        }

                        if (!candidateCodes.Add(code))
                        {
                            return ApiResponse<List<GradeComponentDto>>.Fail("ERR_COMPONENT_CODE_DUPLICATE", StatusCodes.Status400BadRequest);
                        }
                    }
                }

                var payloadIds = suppliedIds.ToHashSet();
                foreach (var omittedSystemComponent in existing.Where(x => x.IsSystem && !payloadIds.Contains(x.Id)))
                {
                    if (!candidateCodes.Add(omittedSystemComponent.Code))
                    {
                        return ApiResponse<List<GradeComponentDto>>.Fail("ERR_COMPONENT_CODE_DUPLICATE", StatusCodes.Status400BadRequest);
                    }
                }

                var effectiveTotalWeight = dto.Components.Sum(x => x.Weight)
                    + existing.Where(x => x.IsSystem && !payloadIds.Contains(x.Id)).Sum(x => x.Weight);
                if (effectiveTotalWeight != 100)
                {
                    return ApiResponse<List<GradeComponentDto>>.Fail("ERR_TOTAL_WEIGHT_MUST_BE_100", StatusCodes.Status400BadRequest);
                }

                using var transaction = await _repository.BeginTransactionAsync();
                var keepIds = dto.Components.Where(x => x.Id.HasValue && x.Id.Value > 0).Select(x => x.Id!.Value).ToHashSet();
                var removed = existing.Where(x => !x.IsSystem && !keepIds.Contains(x.Id)).ToList();

                var toAdd = new List<GradeComponent>();
                var toUpdate = new List<GradeComponent>();

                for (var index = 0; index < dto.Components.Count; index++)
                {
                    var item = dto.Components[index];
                    var code = string.IsNullOrWhiteSpace(item.Code) ? $"custom_{Guid.NewGuid():N}" : item.Code.Trim();

                    var entity = item.Id.HasValue && item.Id.Value > 0 ? existing.FirstOrDefault(x => x.Id == item.Id.Value) : null;
                    if (entity == null)
                    {
                        entity = new GradeComponent { CourseId = courseId, Code = code };
                        toAdd.Add(entity);
                    }
                    else
                    {
                        toUpdate.Add(entity);
                    }

                    entity.Name = item.Name.Trim();
                    if (!entity.IsSystem) entity.Code = code;
                    entity.Weight = item.Weight;
                    entity.SortOrder = item.SortOrder > 0 ? item.SortOrder : index + 1;
                    if (entity.Id == 0) entity.IsSystem = false;
                }

                await _repository.SaveCourseComponentsAsync(courseId, toAdd, toUpdate, removed);

                var newComponents = await _repository.GetComponentsAsync(courseId);
                var result = newComponents.Select(x => MapComponent(x)).ToList();

                return ApiResponse<List<GradeComponentDto>>.Ok(result, "SAVE_GRADE_COMPONENTS_SUCCESS");
            }
            catch (Exception)
            {
                return ApiResponse<List<GradeComponentDto>>.Fail("ERR_INTERNAL_SERVER_ERROR", StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<List<StudentGradeOverrideDto>>> SaveOverridesAsync(int classId, StudentGradeOverridesSaveDto dto)
        {
            try
            {
                var classInfo = await _repository.GetClassInfoAsync(classId);

                if (classInfo == null)
                {
                    return ApiResponse<List<StudentGradeOverrideDto>>.Fail("ERR_CLASS_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                if (!classInfo.Value.CourseId.HasValue)
                {
                    return ApiResponse<List<StudentGradeOverrideDto>>.Fail("ERR_CLASS_COURSE_NOT_FOUND", StatusCodes.Status400BadRequest);
                }

                if (dto?.Overrides == null)
                {
                    return ApiResponse<List<StudentGradeOverrideDto>>.Fail("ERR_GRADE_OVERRIDE_INVALID", StatusCodes.Status400BadRequest);
                }

                var studentClassIds = await _repository.GetStudentClassIdsAsync(classId);
                var components = await _repository.GetComponentsAsync(classInfo.Value.CourseId.Value);
                var componentIds = components.Select(x => x.Id).ToList();

                var duplicateKeys = dto.Overrides
                    .GroupBy(x => new { x.StudentClassId, x.GradeComponentId })
                    .Any(x => x.Count() > 1);
                if (duplicateKeys)
                {
                    return ApiResponse<List<StudentGradeOverrideDto>>.Fail("ERR_GRADE_OVERRIDE_DUPLICATE", StatusCodes.Status400BadRequest);
                }

                if (dto.Overrides.Any(x => !studentClassIds.Contains(x.StudentClassId)))
                {
                    return ApiResponse<List<StudentGradeOverrideDto>>.Fail("ERR_STUDENT_CLASS_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                if (dto.Overrides.Any(x => !componentIds.Contains(x.GradeComponentId)))
                {
                    return ApiResponse<List<StudentGradeOverrideDto>>.Fail("ERR_GRADE_COMPONENT_INVALID", StatusCodes.Status400BadRequest);
                }

                if (dto.Overrides.Any(x => x.Score.HasValue && (x.Score.Value < 0 || x.Score.Value > 10)))
                {
                    return ApiResponse<List<StudentGradeOverrideDto>>.Fail("ERR_GRADE_SCORE_RANGE", StatusCodes.Status400BadRequest);
                }

                using var transaction = await _repository.BeginTransactionAsync();
                var existing = await _repository.GetOverridesAsync(studentClassIds, componentIds);
                var existingByKey = existing.ToDictionary(x => (x.StudentClassId, x.GradeComponentId), x => x);
                var toAdd = new List<StudentGradeOverride>();
                var toUpdate = new List<StudentGradeOverride>();
                var toRemove = new List<StudentGradeOverride>();

                foreach (var item in dto.Overrides)
                {
                    existingByKey.TryGetValue((item.StudentClassId, item.GradeComponentId), out var entity);

                    if (!item.Score.HasValue)
                    {
                        if (entity != null && !entity.IsDeleted)
                        {
                            toRemove.Add(entity);
                        }
                        continue;
                    }

                    var score = item.Score.Value;
                    if (entity == null)
                    {
                        entity = new StudentGradeOverride { StudentClassId = item.StudentClassId, GradeComponentId = item.GradeComponentId };
                        toAdd.Add(entity);
                        existingByKey[(item.StudentClassId, item.GradeComponentId)] = entity;
                    }
                    else
                    {
                        toUpdate.Add(entity);
                    }

                    entity.IsDeleted = false;
                    entity.DeletedAt = null;
                    entity.DeletedBy = null;
                    entity.Score = score;
                }

                await _repository.SaveOverridesAsync(toAdd, toUpdate, toRemove);
                await transaction.CommitAsync();

                var newOverrides = await _repository.GetOverridesAsync(studentClassIds, componentIds);
                var result = newOverrides.Select(x => new StudentGradeOverrideDto
                    {
                        Id = x.Id,
                        StudentClassId = x.StudentClassId,
                        StudentId = x.StudentClass.StudentId,
                        GradeComponentId = x.GradeComponentId,
                        ComponentCode = x.GradeComponent.Code,
                        Score = x.Score
                    })
                    .ToList();

                return ApiResponse<List<StudentGradeOverrideDto>>.Ok(result, "SAVE_GRADE_OVERRIDES_SUCCESS");
            }
            catch (Exception)
            {
                return ApiResponse<List<StudentGradeOverrideDto>>.Fail("ERR_INTERNAL_SERVER_ERROR", StatusCodes.Status500InternalServerError);
            }
        }

        private async Task EnsureDefaultComponentsAsync(int courseId)
        {
            await _repository.EnsureDefaultComponentsAsync(courseId);
        }

        private static decimal NormalizeScore(decimal? score, decimal total)
        {
            if (!score.HasValue || total <= 0) return 0m;
            return Math.Max(0m, Math.Min(10m, score.Value / total * 10m));
        }

        private static decimal Round1(decimal value) => Math.Round(value, 1, MidpointRounding.AwayFromZero);

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
