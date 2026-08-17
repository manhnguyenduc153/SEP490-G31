using sep490_be.Models;
using sep490_be.Repositories.Interfaces;
using sep490_be.Repositories.Common;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace sep490_be.Repositories.Implementations
{
    public class StudentGradeRepository : BaseRepository<StudentGrade, ApplicationDbContext>, IStudentGradeRepository
    {
        public StudentGradeRepository(ApplicationDbContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
        {
        }

        public async Task<(int Id, int? CourseId)?> GetClassInfoAsync(int classId)
        {
            var c = await _dbContext.Classes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == classId && !x.IsDeleted);
            if (c == null) return null;
            return (c.Id, c.CourseId);
        }

        public async Task<List<GradeComponent>> GetComponentsAsync(int courseId)
        {
            return await _dbContext.GradeComponents.Where(x => x.CourseId == courseId).OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync();
        }

        public async Task<List<int>> GetStudentClassIdsAsync(int classId)
        {
            return await _dbContext.StudentClasses.Where(sc => sc.ClassId == classId).Select(sc => sc.Id).ToListAsync();
        }

        public async Task<List<StudentGradeOverride>> GetOverridesAsync(List<int> studentClassIds, List<int> componentIds)
        {
            return await _dbContext.StudentGradeOverrides.Include(x => x.StudentClass).Include(x => x.GradeComponent)
                .Where(x => studentClassIds.Contains(x.StudentClassId) && componentIds.Contains(x.GradeComponentId))
                .OrderBy(x => x.StudentClassId).ThenBy(x => x.GradeComponent.SortOrder).ToListAsync();
        }

        public async Task<List<sep490_be.Models.StudentClass>> GetStudentEnrollmentsAsync(int studentId)
        {
            return await _dbContext.StudentClasses.AsNoTracking().Include(sc => sc.Class).ThenInclude(c => c.Course)
                .Where(sc => sc.StudentId == studentId && !sc.Class.IsDeleted).OrderByDescending(sc => sc.EnrollDate).ThenBy(sc => sc.Class.Name).ToListAsync();
        }

        public async Task<Dictionary<int, decimal?>> GetStudentOverridesAsync(int studentClassId, List<int> componentIds)
        {
            return await _dbContext.StudentGradeOverrides.AsNoTracking()
                .Where(x => x.StudentClassId == studentClassId && componentIds.Contains(x.GradeComponentId))
                .ToDictionaryAsync(x => x.GradeComponentId, x => (decimal?)x.Score);
        }

        public async Task<decimal> CalculateAttendanceScoreAsync(int classId, int studentId)
        {
            var totalSessions = await _dbContext.ClassSchedules.AsNoTracking().CountAsync(x => x.ClassId == classId);
            if (totalSessions == 0) return 0m;
            var attendances = await _dbContext.Attendances.AsNoTracking().Include(x => x.ClassSchedule)
                .Where(x => x.StudentId == studentId && x.ClassSchedule != null && x.ClassSchedule.ClassId == classId && x.Status != -1)
                .Select(x => x.Status).ToListAsync();
            var attended = attendances.Count(x => x > 0);
            return (decimal)attended / totalSessions * 10m;
        }

        public async Task<Dictionary<string, decimal>> CalculateExamSkillScoresAsync(int classId, int studentId)
        {
            var exams = await _dbContext.Exams.AsNoTracking()
                .Include(e => e.ExamQuestions)
                    .ThenInclude(eq => eq.Question)
                .Include(e => e.ExamAttempts)
                .Where(e => e.ClassId == classId)
                .ToListAsync();

            var scoresBySkill = new Dictionary<string, List<decimal>>(StringComparer.OrdinalIgnoreCase)
            {
                ["listening"] = new List<decimal>(),
                ["reading"] = new List<decimal>(),
                ["speaking"] = new List<decimal>(),
                ["writing"] = new List<decimal>()
            };

            foreach (var exam in exams)
            {
                var skillCode = GetExamSkillCode(exam);
                if (skillCode == null) continue;

                var bestScore = exam.ExamAttempts
                    .Where(attempt => attempt.StudentId == studentId)
                    .Select(attempt => NormalizeScore(attempt.Score, exam.TotalScore ?? 10m))
                    .DefaultIfEmpty(0m)
                    .Max();

                scoresBySkill[skillCode].Add(bestScore);
            }

            return scoresBySkill.ToDictionary(
                item => item.Key,
                item => item.Value.Count > 0 ? item.Value.Sum() / item.Value.Count : 0m,
                StringComparer.OrdinalIgnoreCase);
        }

        private static string? GetExamSkillCode(Exam exam)
        {
            var skillTypes = exam.ExamQuestions
                .Select(eq => eq.Question?.SkillType)
                .Where(skillType => skillType.HasValue)
                .Select(skillType => skillType!.Value)
                .Distinct()
                .ToList();

            if (skillTypes.Count != 1) return null;

            return skillTypes[0] switch
            {
                1 => "listening",
                2 => "reading",
                3 => "speaking",
                4 => "writing",
                _ => null
            };
        }

        private static decimal NormalizeScore(decimal? score, decimal total)
        {
            if (!score.HasValue || total <= 0) return 0m;
            return Math.Max(0m, Math.Min(10m, score.Value / total * 10m));
        }

        public async Task<Student?> ResolveStudentByIdentifiersAsync(IEnumerable<string> identifiers, HashSet<string> lookupSet)
        {
            var lookup = identifiers.ToList();
            var student = await _dbContext.Students.AsNoTracking().FirstOrDefaultAsync(s => (s.Email != null && lookup.Contains(s.Email)) || (s.Code != null && lookup.Contains(s.Code)));
            if (student == null)
            {
                var candidates = await _dbContext.Students.AsNoTracking().Where(s => s.Email != null || s.Code != null).ToListAsync();
                student = candidates.FirstOrDefault(s => (!string.IsNullOrWhiteSpace(s.Email) && (lookupSet.Contains(s.Email) || lookupSet.Contains(s.Email.Split('@')[0]))) || (!string.IsNullOrWhiteSpace(s.Code) && lookupSet.Contains(s.Code)));
            }
            return student;
        }

        public async Task<bool> IsParentOfStudentAsync(string email, int studentId)
        {
            return await _dbContext.ParentStudentLinks.AnyAsync(l => l.Parent.Email == email && l.StudentId == studentId);
        }

        public async Task<bool> StudentExistsAsync(int studentId)
        {
            return await _dbContext.Students.AnyAsync(s => s.Id == studentId);
        }

        public async Task<bool> CourseExistsAsync(int courseId)
        {
            return await _dbContext.Courses.AnyAsync(c => c.Id == courseId && !c.IsDeleted);
        }

        public async Task<List<GradeComponent>> GetExistingComponentsAsync(int courseId)
        {
            return await _dbContext.GradeComponents.Where(x => x.CourseId == courseId).ToListAsync();
        }

        public async Task EnsureDefaultComponentsAsync(int courseId)
        {
            var existingComponents = await _dbContext.GradeComponents.Where(x => x.CourseId == courseId).ToListAsync();
            if (existingComponents.Count == 0)
            {
                _dbContext.GradeComponents.AddRange(
                    new GradeComponent { CourseId = courseId, Code = "listening", Name = "Listening", Weight = 25m, SortOrder = 1, IsSystem = true },
                    new GradeComponent { CourseId = courseId, Code = "reading", Name = "Reading", Weight = 25m, SortOrder = 2, IsSystem = true },
                    new GradeComponent { CourseId = courseId, Code = "writing", Name = "Writing", Weight = 25m, SortOrder = 3, IsSystem = true },
                    new GradeComponent { CourseId = courseId, Code = "speaking", Name = "Speaking", Weight = 25m, SortOrder = 4, IsSystem = true }
                );
                await _dbContext.SaveChangesAsync();
                return;
            }

            var skills = new[]
            {
                (Code: "listening", Name: "Listening", SortOrder: 1),
                (Code: "reading", Name: "Reading", SortOrder: 2),
                (Code: "writing", Name: "Writing", SortOrder: 3),
                (Code: "speaking", Name: "Speaking", SortOrder: 4)
            };
            var changed = false;

            var legacyExam = existingComponents.FirstOrDefault(x => x.IsSystem && x.Code.Equals("exam", StringComparison.OrdinalIgnoreCase));
            if (legacyExam != null)
            {
                var missingSkills = skills.Where(skill => existingComponents.All(component => !component.Code.Equals(skill.Code, StringComparison.OrdinalIgnoreCase))).ToList();
                var skillWeight = missingSkills.Count > 0 ? legacyExam.Weight / missingSkills.Count : 0m;
                legacyExam.IsDeleted = true;
                foreach (var skill in missingSkills)
                {
                    var component = new GradeComponent
                    {
                        CourseId = courseId,
                        Code = skill.Code,
                        Name = skill.Name,
                        Weight = skillWeight,
                        SortOrder = skill.SortOrder,
                        IsSystem = true
                    };
                    _dbContext.GradeComponents.Add(component);
                    existingComponents.Add(component);
                }
                changed = true;
            }

            var legacyAttendance = existingComponents.FirstOrDefault(x => x.Code.Equals("attendance", StringComparison.OrdinalIgnoreCase));
            if (legacyAttendance != null)
            {
                var redistributedWeight = legacyAttendance.Weight / skills.Length;
                foreach (var skill in skills)
                {
                    var component = existingComponents.FirstOrDefault(x => x.Code.Equals(skill.Code, StringComparison.OrdinalIgnoreCase));
                    if (component == null)
                    {
                        component = new GradeComponent
                        {
                            CourseId = courseId,
                            Code = skill.Code,
                            Name = skill.Name,
                            Weight = redistributedWeight,
                            SortOrder = skill.SortOrder,
                            IsSystem = true
                        };
                        _dbContext.GradeComponents.Add(component);
                        existingComponents.Add(component);
                    }
                    else
                    {
                        component.Weight += redistributedWeight;
                    }
                }

                await _dbContext.StudentGradeOverrides.IgnoreQueryFilters()
                    .Where(x => x.GradeComponentId == legacyAttendance.Id)
                    .ExecuteDeleteAsync();
                await _dbContext.GradeComponents.IgnoreQueryFilters()
                    .Where(x => x.Id == legacyAttendance.Id)
                    .ExecuteDeleteAsync();
                _dbContext.Entry(legacyAttendance).State = EntityState.Detached;
                existingComponents.Remove(legacyAttendance);
                changed = true;
            }

            var legacyHomework = existingComponents.FirstOrDefault(x => x.IsSystem && x.Code.Equals("homework", StringComparison.OrdinalIgnoreCase));
            if (legacyHomework != null)
            {
                await _dbContext.StudentGradeOverrides.IgnoreQueryFilters()
                    .Where(x => x.GradeComponentId == legacyHomework.Id)
                    .ExecuteDeleteAsync();
                await _dbContext.GradeComponents.IgnoreQueryFilters()
                    .Where(x => x.Id == legacyHomework.Id)
                    .ExecuteDeleteAsync();
                _dbContext.Entry(legacyHomework).State = EntityState.Detached;
                existingComponents.Remove(legacyHomework);
                changed = true;
            }

            if (!changed) return;

            foreach (var skill in skills)
            {
                var component = existingComponents.FirstOrDefault(x => x.Code.Equals(skill.Code, StringComparison.OrdinalIgnoreCase));
                if (component == null)
                {
                    component = new GradeComponent
                    {
                        CourseId = courseId,
                        Code = skill.Code,
                        Name = skill.Name,
                        IsSystem = true
                    };
                    _dbContext.GradeComponents.Add(component);
                    existingComponents.Add(component);
                }

                component.Weight = 25m;
                component.SortOrder = skill.SortOrder;
            }

            await _dbContext.SaveChangesAsync();
        }

        public async Task SaveCourseComponentsAsync(int courseId, List<GradeComponent> toAdd, List<GradeComponent> toUpdate, List<GradeComponent> toRemove)
        {
            if (toRemove.Count > 0) _dbContext.GradeComponents.RemoveRange(toRemove);
            if (toAdd.Count > 0) await _dbContext.GradeComponents.AddRangeAsync(toAdd);
            // toUpdate is tracked so savechanges will catch it
            await _dbContext.SaveChangesAsync();
        }

        public async Task SaveOverridesAsync(List<StudentGradeOverride> toAdd, List<StudentGradeOverride> toUpdate, List<StudentGradeOverride> toRemove)
        {
            if (toRemove.Count > 0) _dbContext.StudentGradeOverrides.RemoveRange(toRemove);
            if (toAdd.Count > 0) await _dbContext.StudentGradeOverrides.AddRangeAsync(toAdd);
            await _dbContext.SaveChangesAsync();
        }
    }
}
