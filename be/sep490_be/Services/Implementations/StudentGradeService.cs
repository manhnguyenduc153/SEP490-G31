using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using sep490_be.DTO;
using sep490_be.DTO.StudentGrade;
using sep490_be.Models;
using sep490_be.Services.Interfaces;

namespace sep490_be.Services.Implementations
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

                var student = await _dbContext.Students
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s =>
                        (s.Email != null && lookup.Contains(s.Email)) ||
                        (s.Code != null && lookup.Contains(s.Code)));

                if (student == null)
                {
                    var candidates = await _dbContext.Students
                        .AsNoTracking()
                        .Where(s => s.Email != null || s.Code != null)
                        .ToListAsync();

                    student = candidates.FirstOrDefault(s =>
                        (!string.IsNullOrWhiteSpace(s.Email) &&
                            (lookupSet.Contains(s.Email) || lookupSet.Contains(s.Email.Split('@')[0]))) ||
                        (!string.IsNullOrWhiteSpace(s.Code) && lookupSet.Contains(s.Code)));
                }

                if (student == null)
                {
                    return ApiResponse<List<MyGradeClassDto>>.Fail("ERR_STUDENT_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                var enrollments = await _dbContext.StudentClasses
                    .AsNoTracking()
                    .Include(sc => sc.Class)
                        .ThenInclude(c => c.Course)
                    .Where(sc => sc.StudentId == student.Id && !sc.Class.IsDeleted)
                    .OrderByDescending(sc => sc.EnrollDate)
                    .ThenBy(sc => sc.Class.Name)
                    .ToListAsync();

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

                    var components = await _dbContext.GradeComponents
                        .AsNoTracking()
                        .Where(x => x.CourseId == courseId)
                        .OrderBy(x => x.SortOrder)
                        .ThenBy(x => x.Id)
                        .ToListAsync();

                    var componentIds = components.Select(x => x.Id).ToList();
                    var overrides = await _dbContext.StudentGradeOverrides
                        .AsNoTracking()
                        .Where(x => x.StudentClassId == enrollment.Id && componentIds.Contains(x.GradeComponentId))
                        .ToDictionaryAsync(x => x.GradeComponentId, x => x.Score);

                    var rawScores = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["attendance"] = await CalculateAttendanceScoreAsync(classInfo.Id, student.Id),
                        ["homework"] = await CalculateHomeworkScoreAsync(classInfo.Id, student.Id),
                        ["exam"] = await CalculateExamScoreAsync(classInfo.Id, student.Id)
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
                            Score = Round1(hasOverride ? overrideScore : rawScore),
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

                return ApiResponse<List<MyGradeClassDto>>.Ok(result, "GET_MY_GRADES_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<List<MyGradeClassDto>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
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

        private async Task<decimal> CalculateAttendanceScoreAsync(int classId, int studentId)
        {
            var attendances = await _dbContext.Attendances
                .AsNoTracking()
                .Include(x => x.ClassSchedule)
                .Where(x => x.StudentId == studentId && x.ClassSchedule != null && x.ClassSchedule.ClassId == classId && x.Status != -1)
                .Select(x => x.Status)
                .ToListAsync();

            if (attendances.Count == 0) return 0m;
            var attended = attendances.Count(x => x != 0);
            return (decimal)attended / attendances.Count * 10m;
        }

        private async Task<decimal> CalculateHomeworkScoreAsync(int classId, int studentId)
        {
            var homeworks = await _dbContext.Homeworks
                .AsNoTracking()
                .Where(x => x.ClassId == classId)
                .Select(x => new { x.Id, x.TotalScore })
                .ToListAsync();

            if (homeworks.Count == 0) return 0m;

            var homeworkIds = homeworks.Select(x => x.Id).ToList();
            var submissions = await _dbContext.HomeworkSubmissions
                .AsNoTracking()
                .Where(x => x.StudentId == studentId && homeworkIds.Contains(x.HomeworkId))
                .GroupBy(x => x.HomeworkId)
                .Select(g => new { HomeworkId = g.Key, Score = g.Max(x => x.Score) })
                .ToListAsync();

            var scoreByHomework = submissions.ToDictionary(x => x.HomeworkId, x => x.Score);
            var normalizedScores = homeworks.Select(homework =>
                NormalizeScore(scoreByHomework.GetValueOrDefault(homework.Id), homework.TotalScore));

            return normalizedScores.Sum() / homeworks.Count;
        }

        private async Task<decimal> CalculateExamScoreAsync(int classId, int studentId)
        {
            var exams = await _dbContext.Exams
                .AsNoTracking()
                .Where(x => x.ClassId == classId)
                .Select(x => new { x.Id, x.TotalScore })
                .ToListAsync();

            if (exams.Count == 0) return 0m;

            var examIds = exams.Select(x => x.Id).ToList();
            var attempts = await _dbContext.ExamAttempts
                .AsNoTracking()
                .Where(x => x.StudentId == studentId && examIds.Contains(x.ExamId))
                .GroupBy(x => x.ExamId)
                .Select(g => new { ExamId = g.Key, Score = g.Max(x => x.Score) })
                .ToListAsync();

            var scoreByExam = attempts.ToDictionary(x => x.ExamId, x => x.Score);
            var normalizedScores = exams.Select(exam =>
                NormalizeScore(scoreByExam.GetValueOrDefault(exam.Id), exam.TotalScore ?? 10m));

            return normalizedScores.Sum() / exams.Count;
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
