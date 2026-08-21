using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using sep490_be.Common;
using sep490_be.DTO;
using sep490_be.DTO.Common;
using sep490_be.DTO.Report;
using sep490_be.Models;
using sep490_be.Services.Interfaces;
using sep490_be.Repositories.Interfaces;
using sep490_be.Enums;

namespace sep490_be.Services.Implementations
{
    public class ReportService : IReportService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IStudentGradeRepository _studentGradeRepository;

        public ReportService(ApplicationDbContext dbContext, IStudentGradeRepository studentGradeRepository)
        {
            _dbContext = dbContext;
            _studentGradeRepository = studentGradeRepository;
        }

        public async Task<ApiResponse<ClassAttendanceSheetDto>> GetClassAttendanceSheetAsync(int classId)
        {
            try
            {
                var classEntity = await _dbContext.Classes
                    .Include(c => c.StudentClasses)
                        .ThenInclude(sc => sc!.Student)
                    .FirstOrDefaultAsync(c => c.Id == classId && !c.IsDeleted);

                if (classEntity == null)
                {
                    return ApiResponse<ClassAttendanceSheetDto>.Fail("ERR_CLASS_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                var schedules = await _dbContext.ClassSchedules
                    .Where(cs => cs.ClassId == classId && !cs.IsDeleted)
                    .OrderBy(cs => cs.LessonNo)
                    .ToListAsync();

                var scheduleIds = schedules.Select(s => s.Id).ToList();
                var attendances = await _dbContext.Attendances
                    .Where(a => a.ScheduleId != null && scheduleIds.Contains(a.ScheduleId.Value))
                    .ToListAsync();

                var report = new ClassAttendanceSheetDto
                {
                    ClassId = classId,
                    ClassCode = classEntity.Code,
                    ClassName = classEntity.Name,
                    TotalSessions = schedules.Count
                };

                var completedSessionIds = attendances.Select(a => a.ScheduleId).Distinct().ToList();
                report.CompletedSessions = completedSessionIds.Count;

                foreach (var sch in schedules)
                {
                    report.Sessions.Add(new ClassAttendanceHeaderDto
                    {
                        ScheduleId = sch.Id,
                        LessonNo = sch.LessonNo ?? 0,
                        Date = sch.ScheduleDate?.ToString("yyyy-MM-dd")
                    });
                }

                if (classEntity.StudentClasses != null)
                {
                    double totalClassAttendanceRate = 0;
                    int studentCount = 0;

                    foreach (var sc in classEntity.StudentClasses)
                    {
                        if (sc.Student == null) continue;

                        var row = new ClassAttendanceStudentRowDto
                        {
                            StudentId = sc.Student.Id,
                            StudentCode = sc.Student.Code,
                            StudentName = sc.Student.Name
                        };

                        foreach (var sch in schedules)
                        {
                            var att = attendances.FirstOrDefault(a => a.ScheduleId == sch.Id && a.StudentId == sc.Student.Id);
                            int status = att?.Status ?? -1;
                            
                            row.Attendances.Add(new ClassAttendanceStatusDto
                            {
                                ScheduleId = sch.Id,
                                Status = status,
                                Description = att?.Description
                            });

                            if (status == (int)AttendanceStatus.Present) row.PresentCount++;
                            else if (status == (int)AttendanceStatus.Absent) row.AbsentCount++;
                        }

                        int takenSessions = row.PresentCount + row.AbsentCount;
                        if (takenSessions > 0)
                        {
                            row.AttendanceRate = Math.Round((double)row.PresentCount / takenSessions * 100, 2);
                        }

                        report.Students.Add(row);
                        totalClassAttendanceRate += row.AttendanceRate;
                        studentCount++;
                    }

                    if (studentCount > 0)
                    {
                        report.AverageAttendanceRate = Math.Round(totalClassAttendanceRate / studentCount, 2);
                    }
                }

                return ApiResponse<ClassAttendanceSheetDto>.Ok(report, "GET_ATTENDANCE_REPORT_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<ClassAttendanceSheetDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }
        public async Task<ApiResponse<ExamResultReportDto>> GetExamResultAnalysisAsync(int examId)
        {
            try
            {
                var examEntity = await _dbContext.Exams
                    .Include(e => e.ExamQuestions)
                        .ThenInclude(eq => eq.Question)
                    .FirstOrDefaultAsync(e => e.Id == examId);
                if (examEntity == null)
                {
                    return ApiResponse<ExamResultReportDto>.Fail("ERR_EXAM_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                var examSkillType = IeltsBandScale.GetSingleSkillType(examEntity);
                var isBandGraded = examSkillType is IeltsBandScale.ListeningSkillType or IeltsBandScale.ReadingSkillType
                    or IeltsBandScale.SpeakingSkillType or IeltsBandScale.WritingSkillType;

                var report = new ExamResultReportDto
                {
                    ExamId = examId,
                    ExamTitle = examEntity.Title,
                    TotalScore = isBandGraded ? IeltsBandScale.SpeakingWritingMaxBand : (examEntity.TotalScore ?? 10m),
                    PassingScore = examEntity.PassingScore ?? 5m
                };

                // Find all target students
                var targetStudentIds = new List<int>();
                if (examEntity.ClassId.HasValue)
                {
                    var studentClasses = await _dbContext.StudentClasses
                        .Where(sc => sc.ClassId == examEntity.ClassId.Value)
                        .Select(sc => sc.StudentId)
                        .ToListAsync();
                    targetStudentIds.AddRange(studentClasses);
                }
                else
                {
                    var attemptedIds = await _dbContext.ExamAttempts
                        .Where(ea => ea.ExamId == examId)
                        .Select(ea => ea.StudentId)
                        .Distinct()
                        .ToListAsync();
                    var gradedIds = await _dbContext.StudentGrades
                        .Where(sg => sg.ExamId == examId)
                        .Select(sg => sg.StudentClass.StudentId)
                        .Distinct()
                        .ToListAsync();
                    
                    targetStudentIds = attemptedIds.Union(gradedIds).Distinct().ToList();
                }

                report.TotalStudents = targetStudentIds.Count;

                if (targetStudentIds.Any())
                {
                    var students = await _dbContext.Students
                        .Where(s => targetStudentIds.Contains(s.Id))
                        .ToListAsync();

                    var attempts = await _dbContext.ExamAttempts
                        .Where(ea => ea.ExamId == examId && targetStudentIds.Contains(ea.StudentId))
                        .ToListAsync();

                    var grades = await _dbContext.StudentGrades
                        .Include(sg => sg.StudentClass)
                        .Where(sg => sg.ExamId == examId && targetStudentIds.Contains(sg.StudentClass.StudentId))
                        .ToListAsync();

                    decimal totalScoreSum = 0;

                    foreach (var s in students)
                    {
                        var studentAttempts = attempts.Where(a => a.StudentId == s.Id).ToList();
                        var studentGrade = grades.FirstOrDefault(g => g.StudentClass.StudentId == s.Id);

                        decimal? attemptScore = studentAttempts.Any() ? studentAttempts.Max(a => a.Score) : null;
                        decimal? finalScore = studentGrade?.FinalScore ?? attemptScore;

                        if (finalScore.HasValue && isBandGraded)
                        {
                            finalScore = IeltsBandScale.RoundToHalfBand(finalScore.Value);
                        }

                        var result = new StudentExamResultDto
                        {
                            StudentId = s.Id,
                            StudentCode = s.Code ?? "",
                            StudentName = s.Name ?? "",
                            AttemptCount = studentAttempts.Count,
                            FinalScore = finalScore,
                            SubmittedAt = studentAttempts.OrderByDescending(a => a.SubmitTime).FirstOrDefault()?.SubmitTime
                        };

                        result.IsPassed = result.FinalScore.HasValue && result.FinalScore.Value >= report.PassingScore;

                        report.StudentResults.Add(result);

                        if (result.FinalScore.HasValue)
                        {
                            report.ParticipatedStudents++;
                            totalScoreSum += result.FinalScore.Value;
                            if (result.IsPassed)
                            {
                                report.PassedStudents++;
                            }
                            else
                            {
                                report.FailedStudents++;
                            }
                        }
                    }

                    if (report.ParticipatedStudents > 0)
                    {
                        var avg = totalScoreSum / report.ParticipatedStudents;
                        report.AverageScore = isBandGraded ? IeltsBandScale.RoundToHalfBand(avg) : Math.Round(avg, 2);
                        report.PassRate = Math.Round((decimal)report.PassedStudents / report.ParticipatedStudents * 100, 2);
                    }
                }

                return ApiResponse<ExamResultReportDto>.Ok(report, "GET_EXAM_RESULT_REPORT_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<ExamResultReportDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }
        public async Task<ApiResponse<ClassGradeReportDto>> GetClassGradeReportAsync(int classId)
        {
            try
            {
                var classEntity = await _dbContext.Classes
                    .Include(c => c.Course)
                    .Include(c => c.StudentClasses)
                        .ThenInclude(sc => sc.Student)
                    .FirstOrDefaultAsync(c => c.Id == classId && !c.IsDeleted);

                if (classEntity == null)
                {
                    return ApiResponse<ClassGradeReportDto>.Fail("ERR_CLASS_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                if (classEntity.CourseId == null)
                {
                    return ApiResponse<ClassGradeReportDto>.Fail("ERR_CLASS_HAS_NO_COURSE", StatusCodes.Status400BadRequest);
                }

                var courseId = classEntity.CourseId.Value;
                await _studentGradeRepository.EnsureDefaultComponentsAsync(courseId);

                var report = new ClassGradeReportDto
                {
                    ClassId = classId,
                    ClassCode = classEntity.Code ?? "",
                    ClassName = classEntity.Name ?? ""
                };

                // Lấy cấu trúc điểm (Components) của Course
                var components = await _studentGradeRepository.GetComponentsAsync(courseId);

                foreach (var comp in components)
                {
                    report.Components.Add(new sep490_be.DTO.StudentGrade.GradeComponentDto
                    {
                        Id = comp.Id,
                        CourseId = comp.CourseId,
                        Code = comp.Code ?? "",
                        Name = comp.Name ?? "",
                        Weight = comp.Weight,
                        SortOrder = comp.SortOrder,
                        IsSystem = comp.IsSystem
                    });
                }

                var componentIds = components.Select(c => c.Id).ToList();

                if (classEntity.StudentClasses != null)
                {
                    foreach (var sc in classEntity.StudentClasses.Where(x => x.Student != null && !x.Student.IsDeleted))
                    {
                        var student = sc.Student!;
                        var row = new StudentGradeRowDto
                        {
                            StudentId = student.Id,
                            StudentCode = student.Code ?? "",
                            StudentName = student.Name ?? ""
                        };

                        var overrides = await _studentGradeRepository.GetStudentOverridesAsync(sc.Id, componentIds);
                        var rawScores = await _studentGradeRepository.CalculateExamSkillScoresAsync(classId, student.Id);

                        decimal weightedSum = 0m;
                        decimal totalWeight = 0m;
                        bool hasAnyScore = false;

                        foreach (var comp in components)
                        {
                            var hasOverride = overrides.TryGetValue(comp.Id, out var overrideScore);
                            var hasRawScore = rawScores.TryGetValue(comp.Code, out var rawScore);

                            if (hasOverride && overrideScore.HasValue)
                            {
                                var finalCompScore = IeltsBandScale.RoundToHalfBand(overrideScore.Value);
                                row.ComponentScores[comp.Id] = finalCompScore;
                                weightedSum += finalCompScore * Math.Max(0m, comp.Weight);
                                totalWeight += Math.Max(0m, comp.Weight);
                                hasAnyScore = true;
                            }
                            else if (hasRawScore)
                            {
                                var finalCompScore = IeltsBandScale.RoundToHalfBand(rawScore);
                                row.ComponentScores[comp.Id] = finalCompScore;
                                weightedSum += finalCompScore * Math.Max(0m, comp.Weight);
                                totalWeight += Math.Max(0m, comp.Weight);
                                hasAnyScore = true;
                            }
                            else
                            {
                                row.ComponentScores[comp.Id] = 0m;
                                totalWeight += Math.Max(0m, comp.Weight);
                            }
                        }

                        if (hasAnyScore && totalWeight > 0)
                        {
                            var rawAvg = weightedSum / totalWeight;
                            row.FinalScore = IeltsBandScale.RoundToHalfBand(rawAvg);
                            row.IsPassed = row.FinalScore >= 5.0m;
                        }
                        else
                        {
                            row.FinalScore = 0m;
                            row.IsPassed = false;
                        }

                        report.Students.Add(row);
                    }
                }

                return ApiResponse<ClassGradeReportDto>.Ok(report, "GET_CLASS_GRADE_REPORT_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<ClassGradeReportDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }
    }
}
