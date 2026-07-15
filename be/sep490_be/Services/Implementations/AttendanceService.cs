using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using sep490_be.DTO;
using sep490_be.DTO.Attendance;
using sep490_be.Models;
using sep490_be.Repositories.Interfaces;
using sep490_be.Services.Interfaces;
using sep490_be.Enums;

namespace sep490_be.Services.Implementations
{
    public class AttendanceService : IAttendanceService
    {
        private readonly IAttendanceRepository _repository;
        private readonly ApplicationDbContext _dbContext;

        public AttendanceService(IAttendanceRepository repository, ApplicationDbContext dbContext)
        {
            _repository = repository;
            _dbContext = dbContext;
        }

        public async Task<ApiResponse<List<AttendanceDto>>> GetByScheduleIdAsync(int scheduleId)
        {
            try
            {
                // Check if schedule exists
                var schedule = await _dbContext.ClassSchedules
                    .Include(cs => cs.Class)
                        .ThenInclude(c => c!.StudentClasses)
                            .ThenInclude(sc => sc!.Student)
                    .FirstOrDefaultAsync(cs => cs.Id == scheduleId);

                if (schedule == null)
                {
                    return ApiResponse<List<AttendanceDto>>.Fail("ERR_SCHEDULE_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                // Fetch attendance records from database
                var existingAttendances = await _repository.FindAll()
                    .Include(a => a.Student)
                    .Where(a => a.ScheduleId == scheduleId)
                    .ToListAsync();

                var dtos = new List<AttendanceDto>();

                if (existingAttendances.Any())
                {
                    dtos = existingAttendances.Select(a => new AttendanceDto
                    {
                        Id = a.Id,
                        ScheduleId = a.ScheduleId,
                        StudentId = a.StudentId,
                        StudentCode = a.Student?.Code,
                        StudentName = a.Student?.Name,
                        Status = a.Status,
                        StatusName = ((AttendanceStatus)a.Status).GetStringValue(),
                        CheckInTime = a.CheckInTime,
                        Description = a.Description
                    }).ToList();
                }
                else
                {
                    // Dynamic fallback: construct defaults from enrolled class students list
                    if (schedule.Class?.StudentClasses != null)
                    {
                        foreach (var sc in schedule.Class.StudentClasses)
                        {
                            if (sc.Student != null)
                            {
                                dtos.Add(new AttendanceDto
                                {
                                    Id = 0,
                                    ScheduleId = scheduleId,
                                    StudentId = sc.Student.Id,
                                    StudentCode = sc.Student.Code,
                                    StudentName = sc.Student.Name,
                                    Status = 1, // Default: Present
                                    StatusName = AttendanceStatus.Present.GetStringValue(),
                                    CheckInTime = null,
                                    Description = null
                                });
                            }
                        }
                    }
                }

                return ApiResponse<List<AttendanceDto>>.Ok(dtos, "GET_ATTENDANCE_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<List<AttendanceDto>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<bool>> BulkSaveAsync(AttendanceBulkSaveDto dto)
        {
            try
            {
                // Check if schedule exists
                var schedule = await _dbContext.ClassSchedules
                    .FirstOrDefaultAsync(cs => cs.Id == dto.ScheduleId);

                if (schedule == null)
                {
                    return ApiResponse<bool>.Fail("ERR_SCHEDULE_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                // Fetch existing attendances to update
                var existingAttendances = await _repository.FindAll()
                    .Where(a => a.ScheduleId == dto.ScheduleId)
                    .ToListAsync();

                foreach (var attSave in dto.Attendances)
                {
                    var existing = existingAttendances.FirstOrDefault(a => a.StudentId == attSave.StudentId);
                    if (existing != null)
                    {
                        existing.Status = attSave.Status;
                        existing.Description = attSave.Description;
                        existing.CheckInTime = DateTime.UtcNow;

                        await _repository.UpdateAsync(existing);
                    }
                    else
                    {
                        var newAttendance = new Models.Attendance
                        {
                            ScheduleId = dto.ScheduleId,
                            StudentId = attSave.StudentId,
                            Status = attSave.Status,
                            Description = attSave.Description,
                            CheckInTime = DateTime.UtcNow
                        };
                        await _repository.AddAsync(newAttendance);
                    }
                }

                await _repository.SaveChangesAsync();
                return ApiResponse<bool>.Ok(true, "SAVE_ATTENDANCE_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<AttendanceReportDto>> GetReportByClassIdAsync(int classId)
        {
            try
            {
                var classEntity = await _dbContext.Classes
                    .Include(c => c.StudentClasses)
                        .ThenInclude(sc => sc!.Student)
                    .FirstOrDefaultAsync(c => c.Id == classId);

                if (classEntity == null)
                {
                    return ApiResponse<AttendanceReportDto>.Fail("ERR_CLASS_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                var schedules = await _dbContext.ClassSchedules
                    .Where(cs => cs.ClassId == classId)
                    .OrderBy(cs => cs.LessonNo)
                    .ToListAsync();

                var scheduleIds = schedules.Select(s => s.Id).ToList();
                var attendances = await _repository.FindAll()
                    .Where(a => a.ScheduleId != null && scheduleIds.Contains(a.ScheduleId.Value))
                    .ToListAsync();

                var report = new AttendanceReportDto();

                foreach (var sch in schedules)
                {
                    report.Sessions.Add(new AttendanceReportHeaderDto
                    {
                        ScheduleId = sch.Id,
                        LessonNo = sch.LessonNo ?? 0,
                        Date = sch.ScheduleDate?.ToString("yyyy-MM-dd")
                    });
                }

                if (classEntity.StudentClasses != null)
                {
                    foreach (var sc in classEntity.StudentClasses)
                    {
                        if (sc.Student == null) continue;

                        var row = new AttendanceReportStudentRowDto
                        {
                            StudentId = sc.Student.Id,
                            StudentCode = sc.Student.Code,
                            StudentName = sc.Student.Name
                        };

                        foreach (var sch in schedules)
                        {
                            var att = attendances.FirstOrDefault(a => a.ScheduleId == sch.Id && a.StudentId == sc.Student.Id);
                            row.Attendances.Add(new AttendanceReportStatusDto
                            {
                                ScheduleId = sch.Id,
                                Status = att?.Status ?? -1,
                                Description = att?.Description
                            });
                        }

                        report.Students.Add(row);
                    }
                }

                return ApiResponse<AttendanceReportDto>.Ok(report, "GET_ATTENDANCE_REPORT_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<AttendanceReportDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<List<MyAttendanceClassDto>>> GetMyAttendanceAsync(IEnumerable<string> identifiers)
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
                    return ApiResponse<List<MyAttendanceClassDto>>.Fail("ERR_STUDENT_NOT_FOUND", StatusCodes.Status404NotFound);
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
                    return ApiResponse<List<MyAttendanceClassDto>>.Fail("ERR_STUDENT_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                var studentId = student.Id;
                var result = await _dbContext.StudentClasses
                    .AsNoTracking()
                    .Where(sc => sc.StudentId == studentId &&
                                 sc.Class.Status == 1 &&
                                 !sc.Class.IsDeleted)
                    .OrderBy(sc => sc.Class.Name)
                    .Select(sc => new MyAttendanceClassDto
                    {
                        ClassId = sc.ClassId,
                        ClassCode = sc.Class.Code,
                        ClassName = sc.Class.Name,
                        CourseName = sc.Class.Course != null ? sc.Class.Course.Name : null,
                        TeacherName = sc.Class.Teacher != null ? sc.Class.Teacher.Name : null,
                        AttendedSessions = sc.Class.ClassSchedules
                            .Where(schedule => !schedule.IsDeleted)
                            .SelectMany(schedule => schedule.Attendances)
                            .Count(attendance => attendance.StudentId == studentId && attendance.Status != (int)AttendanceStatus.Absent),
                        AbsentSessions = sc.Class.ClassSchedules
                            .Where(schedule => !schedule.IsDeleted)
                            .SelectMany(schedule => schedule.Attendances)
                            .Count(attendance => attendance.StudentId == studentId && attendance.Status == (int)AttendanceStatus.Absent),
                        TotalSessions = sc.Class.ClassSchedules
                            .Count(schedule => !schedule.IsDeleted)
                    })
                    .ToListAsync();

                foreach (var item in result)
                {
                    item.AttendanceRate = item.TotalSessions > 0
                        ? Math.Round((double)item.AttendedSessions / item.TotalSessions * 100, 1)
                        : 0;
                }

                return ApiResponse<List<MyAttendanceClassDto>>.Ok(result, "GET_MY_ATTENDANCE_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<List<MyAttendanceClassDto>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<List<MyAttendanceSessionDto>>> GetMyAttendanceDetailsAsync(int classId, IEnumerable<string> identifiers)
        {
            try
            {
                var lookup = identifiers
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Distinct()
                    .ToList();
                var lookupSet = lookup.ToHashSet(StringComparer.OrdinalIgnoreCase);

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
                    return ApiResponse<List<MyAttendanceSessionDto>>.Fail("ERR_STUDENT_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                var isEnrolled = await _dbContext.StudentClasses
                    .AsNoTracking()
                    .AnyAsync(sc => sc.StudentId == student.Id &&
                                    sc.ClassId == classId &&
                                    sc.Class.Status == 1 &&
                                    !sc.Class.IsDeleted);

                if (!isEnrolled)
                {
                    return ApiResponse<List<MyAttendanceSessionDto>>.Fail("ERR_CLASS_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                var sessions = await _dbContext.Attendances
                    .AsNoTracking()
                    .Where(a => a.StudentId == student.Id &&
                                a.ClassSchedule != null &&
                                a.ClassSchedule.ClassId == classId)
                    .OrderBy(a => a.ClassSchedule!.ScheduleDate)
                    .ThenBy(a => a.ClassSchedule!.LessonNo)
                    .Select(a => new MyAttendanceSessionDto
                    {
                        ScheduleId = a.ScheduleId ?? 0,
                        LessonNo = a.ClassSchedule!.LessonNo ?? 0,
                        Date = a.ClassSchedule.ScheduleDate,
                        Status = a.Status,
                        Description = a.Description
                    })
                    .ToListAsync();

                foreach (var session in sessions)
                {
                    session.StatusName = ((AttendanceStatus)session.Status).GetStringValue();
                }

                return ApiResponse<List<MyAttendanceSessionDto>>.Ok(sessions, "GET_MY_ATTENDANCE_DETAILS_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<List<MyAttendanceSessionDto>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }
    }
}

