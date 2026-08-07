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
        public AttendanceService(IAttendanceRepository repository)
        {
            _repository = repository;
        }

        public async Task<ApiResponse<List<AttendanceDto>>> GetByScheduleIdAsync(int scheduleId)
        {
            try
            {
                // Check if schedule exists
                var schedule = await _repository.GetScheduleWithAttendancesAsync(scheduleId);

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
                var schedule = await _repository.GetScheduleAsync(dto.ScheduleId);

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
                var classEntity = await _repository.GetClassWithStudentClassesAsync(classId);

                if (classEntity == null)
                {
                    return ApiResponse<AttendanceReportDto>.Fail("ERR_CLASS_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                var schedules = await _repository.GetSchedulesByClassIdAsync(classId);

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

                var student = await _repository.GetStudentByIdentifiersAsync(lookup, lookupSet);

                if (student == null)
                {
                    return ApiResponse<List<MyAttendanceClassDto>>.Fail("ERR_STUDENT_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                var studentId = student.Id;
                var result = await _repository.GetStudentClassAttendancesAsync(studentId);

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

                var student = await _repository.GetStudentByIdentifiersAsync(lookup, lookupSet);

                if (student == null)
                {
                    return ApiResponse<List<MyAttendanceSessionDto>>.Fail("ERR_STUDENT_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                var isEnrolled = await _repository.IsStudentEnrolledInClassAsync(student.Id, classId);

                if (!isEnrolled)
                {
                    return ApiResponse<List<MyAttendanceSessionDto>>.Fail("ERR_CLASS_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                var result = await _repository.GetStudentAttendanceSessionsAsync(classId, student.Id);

                return ApiResponse<List<MyAttendanceSessionDto>>.Ok(result, "GET_MY_ATTENDANCE_DETAILS_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<List<MyAttendanceSessionDto>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }
    }
}

