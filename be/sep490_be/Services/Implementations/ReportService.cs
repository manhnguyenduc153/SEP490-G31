using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using sep490_be.DTO;
using sep490_be.DTO.Common;
using sep490_be.DTO.Report;
using sep490_be.Models;
using sep490_be.Services.Interfaces;
using sep490_be.Enums;

namespace sep490_be.Services.Implementations
{
    public class ReportService : IReportService
    {
        private readonly ApplicationDbContext _dbContext;

        public ReportService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<ApiResponse<ClassAttendanceSheetDto>> GetClassAttendanceSheetAsync(int classId)
        {
            try
            {
                var classEntity = await _dbContext.Classes
                    .Include(c => c.StudentClasses)
                        .ThenInclude(sc => sc!.Student)
                    .FirstOrDefaultAsync(c => c.Id == classId);

                if (classEntity == null)
                {
                    return ApiResponse<ClassAttendanceSheetDto>.Fail("ERR_CLASS_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                var schedules = await _dbContext.ClassSchedules
                    .Where(cs => cs.ClassId == classId)
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
                            else if (status == (int)AttendanceStatus.Late) row.LateCount++;
                            else if (status == (int)AttendanceStatus.Excused) row.ExcusedCount++;
                        }

                        int takenSessions = row.PresentCount + row.AbsentCount + row.LateCount + row.ExcusedCount;
                        if (takenSessions > 0)
                        {
                            row.AttendanceRate = Math.Round((double)(row.PresentCount + row.LateCount) / takenSessions * 100, 2);
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
                var examEntity = await _dbContext.Exams.FirstOrDefaultAsync(e => e.Id == examId);
                if (examEntity == null)
                {
                    return ApiResponse<ExamResultReportDto>.Fail("ERR_EXAM_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                var report = new ExamResultReportDto
                {
                    ExamId = examId,
                    ExamTitle = examEntity.Title,
                    TotalScore = examEntity.TotalScore ?? 10m,
                    PassingScore = examEntity.PassingScore ?? 5m
                };

                // Find all target students
                var targetStudentIds = new List<int>();
                if (examEntity.ClassId.HasValue)
                {
                    // Students in the class
                    var studentClasses = await _dbContext.StudentClasses
                        .Where(sc => sc.ClassId == examEntity.ClassId.Value)
                        .Select(sc => sc.StudentId)
                        .ToListAsync();
                    targetStudentIds.AddRange(studentClasses);
                }
                else
                {
                    // If no class linked, get all students who attempted or have a grade
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

                        var result = new StudentExamResultDto
                        {
                            StudentId = s.Id,
                            StudentCode = s.Code ?? "",
                            StudentName = s.Name ?? "",
                            AttemptCount = studentAttempts.Count,
                            FinalScore = studentGrade?.FinalScore ?? (studentAttempts.Any() ? studentAttempts.Max(a => a.Score) : null),
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
                        report.AverageScore = Math.Round(totalScoreSum / report.ParticipatedStudents, 2);
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
                    .FirstOrDefaultAsync(c => c.Id == classId);

                if (classEntity == null)
                {
                    return ApiResponse<ClassGradeReportDto>.Fail("ERR_CLASS_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                if (classEntity.CourseId == null)
                {
                    return ApiResponse<ClassGradeReportDto>.Fail("ERR_CLASS_HAS_NO_COURSE", StatusCodes.Status400BadRequest);
                }

                var report = new ClassGradeReportDto
                {
                    ClassId = classId,
                    ClassCode = classEntity.Code ?? "",
                    ClassName = classEntity.Name ?? ""
                };

                // Lấy cấu trúc điểm (Components) của Course
                var components = await _dbContext.GradeComponents
                    .Where(gc => gc.CourseId == classEntity.CourseId)
                    .OrderBy(gc => gc.SortOrder)
                    .ToListAsync();

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

                // Lấy toàn bộ điểm Override của học sinh trong lớp
                var overrides = await _dbContext.StudentGradeOverrides
                    .Include(o => o.StudentClass)
                    .Where(o => o.StudentClass.ClassId == classId)
                    .ToListAsync();

                if (classEntity.StudentClasses != null)
                {
                    foreach (var sc in classEntity.StudentClasses)
                    {
                        if (sc.Student == null) continue;

                        var row = new StudentGradeRowDto
                        {
                            StudentId = sc.Student.Id,
                            StudentCode = sc.Student.Code ?? "",
                            StudentName = sc.Student.Name ?? ""
                        };

                        decimal finalScore = 0;
                        decimal totalWeight = 0;
                        bool hasAnyScore = false;

                        foreach (var comp in components)
                        {
                            var overrideScore = overrides.FirstOrDefault(o => o.StudentClassId == sc.Id && o.GradeComponentId == comp.Id);
                            if (overrideScore != null)
                            {
                                row.ComponentScores[comp.Id] = overrideScore.Score;
                                finalScore += overrideScore.Score * (comp.Weight / 100m);
                                totalWeight += comp.Weight;
                                hasAnyScore = true;
                            }
                            else
                            {
                                row.ComponentScores[comp.Id] = null;
                            }
                        }

                        if (hasAnyScore)
                        {
                            row.FinalScore = Math.Round(finalScore, 2);
                            row.IsPassed = row.FinalScore >= 5.0m;
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
