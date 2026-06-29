using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Google.OrTools.Sat;
using PRN232_be.DTO;
using PRN232_be.DTO.Class;
using PRN232_be.Models;
using PRN232_be.Services.Interfaces;
using PRN232_be.Enums;

namespace PRN232_be.Services.Implementations
{
    public class ScheduleOptimizationService : IScheduleOptimizationService
    {
        private readonly ApplicationDbContext _dbContext;

        public ScheduleOptimizationService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<ApiResponse<ConflictCheckResultDto>> CheckConflictAsync(ClassSaveDto dto)
        {
            try
            {
                var result = new ConflictCheckResultDto { HasConflict = false };

                if (dto.WeeklySchedules == null || !dto.WeeklySchedules.Any() || 
                    !dto.StartDate.HasValue || !dto.ExpectedLessons.HasValue || dto.ExpectedLessons.Value <= 0)
                {
                    return ApiResponse<ConflictCheckResultDto>.Ok(result, "NO_SCHEDULE_DATA_TO_CHECK");
                }

                // Generate candidate schedules for the class in memory
                var currentDate = dto.StartDate.Value;
                int lessonNo = 1;
                var weeklySchedules = dto.WeeklySchedules.OrderBy(w => w.DayOfWeek).ToList();
                var proposedSchedules = new List<ProposedScheduleTemp>();

                // Guard against invalid DayOfWeek values
                if (weeklySchedules.Any(w => w.DayOfWeek < 0 || w.DayOfWeek > 6))
                {
                    return ApiResponse<ConflictCheckResultDto>.Fail("ERR_INVALID_DAY_OF_WEEK", StatusCodes.Status400BadRequest);
                }

                while (lessonNo <= dto.ExpectedLessons.Value)
                {
                    var match = weeklySchedules.FirstOrDefault(w => (int)currentDate.DayOfWeek == w.DayOfWeek);
                    if (match != null)
                    {
                        if (TimeSpan.TryParse(match.StartTime, out var startSpan) && 
                            TimeSpan.TryParse(match.EndTime, out var endSpan))
                        {
                            proposedSchedules.Add(new ProposedScheduleTemp
                            {
                                LessonNo = lessonNo,
                                Date = currentDate,
                                StartTime = startSpan,
                                EndTime = endSpan,
                                RoomId = match.RoomId
                            });
                            lessonNo++;
                        }
                    }
                    currentDate = currentDate.AddDays(1);
                }

                if (!proposedSchedules.Any())
                {
                    return ApiResponse<ConflictCheckResultDto>.Ok(result, "NO_PROPOSED_SCHEDULES_GENERATED");
                }

                var minDate = proposedSchedules.Min(p => p.Date);
                var maxDate = proposedSchedules.Max(p => p.Date);

                // Fetch existing schedules in the database for the given date range (except this class)
                var existingSchedules = await _dbContext.ClassSchedules
                    .Include(cs => cs.Class)
                    .Include(cs => cs.TimeSlot)
                    .Include(cs => cs.Room)
                    .Include(cs => cs.Teacher)
                    .Where(cs => cs.Class != null && !cs.Class.IsDeleted && cs.ClassId != dto.Id)
                    .Where(cs => cs.ScheduleDate >= minDate && cs.ScheduleDate <= maxDate)
                    .ToListAsync();

                var conflicts = new List<ConflictDetailDto>();

                foreach (var prop in proposedSchedules)
                {
                    foreach (var ext in existingSchedules)
                    {
                        if (ext.ScheduleDate?.Date != prop.Date.Date) continue;

                        // Check time overlap
                        bool timeOverlaps = ext.TimeSlot != null && 
                                           ext.TimeSlot.StartTime < prop.EndTime && 
                                           ext.TimeSlot.EndTime > prop.StartTime;
                        if (!timeOverlaps) continue;

                        // 1. Teacher conflict
                        if (dto.TeacherId.HasValue && ext.TeacherId == dto.TeacherId.Value)
                        {
                            conflicts.Add(new ConflictDetailDto
                            {
                                Type = "Teacher",
                                TeacherId = dto.TeacherId,
                                TeacherName = ext.Teacher?.Name,
                                Date = prop.Date,
                                StartTime = prop.StartTime.ToString(@"hh\:mm"),
                                EndTime = prop.EndTime.ToString(@"hh\:mm"),
                                SlotId = ext.SlotId,
                                SlotName = ext.TimeSlot?.Name,
                                ConflictClassId = ext.ClassId,
                                ConflictClassCode = ext.Class?.Code,
                                ConflictClassName = ext.Class?.Name
                            });
                        }

                        // 2. Room conflict
                        if (prop.RoomId.HasValue && ext.RoomId == prop.RoomId.Value)
                        {
                            conflicts.Add(new ConflictDetailDto
                            {
                                Type = "Room",
                                RoomId = prop.RoomId,
                                RoomName = ext.Room?.Name,
                                Date = prop.Date,
                                StartTime = prop.StartTime.ToString(@"hh\:mm"),
                                EndTime = prop.EndTime.ToString(@"hh\:mm"),
                                SlotId = ext.SlotId,
                                SlotName = ext.TimeSlot?.Name,
                                ConflictClassId = ext.ClassId,
                                ConflictClassCode = ext.Class?.Code,
                                ConflictClassName = ext.Class?.Name
                            });
                        }
                    }
                }

                if (conflicts.Any())
                {
                    result.HasConflict = true;
                    result.Conflicts = conflicts.OrderBy(c => c.Date).ThenBy(c => c.StartTime).ToList();
                }

                return ApiResponse<ConflictCheckResultDto>.Ok(result, "CONFLICT_CHECK_COMPLETED");
            }
            catch (Exception ex)
            {
                return ApiResponse<ConflictCheckResultDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<List<ClassDto>>> AutoScheduleAsync(List<int> classIds)
        {
            try
            {
                if (classIds == null || !classIds.Any())
                {
                    return ApiResponse<List<ClassDto>>.Fail("ERR_NO_CLASSES_SELECTED", StatusCodes.Status400BadRequest);
                }

                // 1. Fetch data
                var classes = await _dbContext.Classes
                    .Include(c => c.Course)
                    .Include(c => c.Teacher)
                    .Include(c => c.ClassSchedules)
                    .Include(c => c.StudentClasses)
                    .Where(c => classIds.Contains(c.Id) && !c.IsDeleted)
                    .ToListAsync();

                if (!classes.Any())
                {
                    return ApiResponse<List<ClassDto>>.Fail("ERR_CLASSES_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                var teachers = await _dbContext.Teachers
                    .Where(t => t.Status == (int)TeacherStatus.Active && !t.IsDeleted)
                    .ToListAsync();

                var rooms = await _dbContext.Rooms
                    .Where(r => r.Status == (int)RoomStatus.Active && !r.IsDeleted)
                    .ToListAsync();

                var timeSlots = await _dbContext.TimeSlots
                    .Where(ts => !ts.IsDeleted)
                    .ToListAsync();

                if (!teachers.Any())
                {
                    return ApiResponse<List<ClassDto>>.Fail("ERR_NO_ACTIVE_TEACHERS", StatusCodes.Status400BadRequest);
                }

                if (!rooms.Any())
                {
                    return ApiResponse<List<ClassDto>>.Fail("ERR_NO_ACTIVE_ROOMS", StatusCodes.Status400BadRequest);
                }

                if (!timeSlots.Any())
                {
                    return ApiResponse<List<ClassDto>>.Fail("ERR_NO_TIMESLOTS", StatusCodes.Status400BadRequest);
                }

                // Check validation for each selected class
                foreach (var c in classes)
                {
                    if (c.CourseId == null)
                        return ApiResponse<List<ClassDto>>.Fail($"ERR_CLASS_NO_COURSE_{c.Code}", StatusCodes.Status400BadRequest);
                    if (c.StudentClasses == null || !c.StudentClasses.Any())
                        return ApiResponse<List<ClassDto>>.Fail($"ERR_CLASS_NO_STUDENTS_{c.Code}", StatusCodes.Status400BadRequest);
                }

                // 2. FIXED 5 slots (hard-coded, source of truth) ───────────────────────────
                var fixedSlots  = FixedTimeSlot.All;
                int numFixed    = fixedSlots.Length;
                int numClasses  = classes.Count;
                int numTeachers = teachers.Count;
                int numRooms    = rooms.Count;

                // Parse frequency for each class (number of weekly sessions)
                var frequencies = new int[numClasses];
                var existingWS  = new List<WeeklyScheduleDto>[numClasses];
                var jsonOpts    = new System.Text.Json.JsonSerializerOptions
                    { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };

                for (int i = 0; i < numClasses; i++)
                {
                    frequencies[i] = 2;
                    existingWS[i]  = new List<WeeklyScheduleDto>();
                    if (!string.IsNullOrEmpty(classes[i].WeeklySchedulesJson))
                    {
                        try
                        {
                            var list = System.Text.Json.JsonSerializer.Deserialize<List<WeeklyScheduleDto>>(classes[i].WeeklySchedulesJson, jsonOpts);
                            if (list != null && list.Any()) { existingWS[i] = list; frequencies[i] = list.Count; }
                        }
                        catch { /* ignore */ }
                    }
                }

                int maxSessions = frequencies.Max();

                // 3. CP-SAT model ──────────────────────────────────────────────────────────
                var model = new CpModel();

                // Decision variables
                var teacherVar   = new IntVar[numClasses];
                var roomVar      = new IntVar[numClasses];
                var dayVar       = new IntVar[numClasses, maxSessions]; // 1=Mon..6=Sat
                var slotIndexVar = new IntVar[numClasses, maxSessions]; // 0..numFixed-1
                var slotVar      = new IntVar[numClasses, maxSessions]; // flat = day*numFixed + slotIndex

                for (int i = 0; i < numClasses; i++)
                {
                    teacherVar[i] = model.NewIntVar(0, numTeachers - 1, $"t_{i}");
                    roomVar[i]    = model.NewIntVar(0, numRooms - 1,    $"r_{i}");

                    // Pin teacher only if already assigned and still active
                    if (classes[i].TeacherId.HasValue)
                    {
                        int tIdx = teachers.FindIndex(t => t.Id == classes[i].TeacherId.Value);
                        if (tIdx >= 0) model.Add(teacherVar[i] == tIdx);
                    }

                    int freq = frequencies[i];
                    for (int j = 0; j < maxSessions; j++)
                    {
                        if (j < freq)
                        {
                            dayVar[i, j]       = model.NewIntVar(1, 6, $"day_{i}_{j}");
                            slotIndexVar[i, j] = model.NewIntVar(0, numFixed - 1, $"fs_{i}_{j}");
                            slotVar[i, j]      = model.NewIntVar(0, 7 * numFixed - 1, $"flat_{i}_{j}");
                            // flat relationship
                            model.Add(slotVar[i, j] == dayVar[i, j] * numFixed + slotIndexVar[i, j]);

                            // Pin if existing weekly schedule has this session
                            if (j < existingWS[i].Count)
                            {
                                var ws = existingWS[i][j];
                                if (ws.DayOfWeek >= 1 && ws.DayOfWeek <= 6)
                                    model.Add(dayVar[i, j] == ws.DayOfWeek);
                                if (TimeSpan.TryParse(ws.StartTime, out var st))
                                {
                                    int fi = Array.FindIndex(fixedSlots, fs => fs.Start == st);
                                    if (fi >= 0) model.Add(slotIndexVar[i, j] == fi);
                                }
                            }
                        }
                        else
                        {
                            dayVar[i, j]       = model.NewConstant(-1);
                            slotIndexVar[i, j] = model.NewConstant(-1);
                            slotVar[i, j]      = model.NewConstant(-1);
                        }
                    }

                    // Sessions of the same class must be on different days (ascending breaks symmetry)
                    for (int j = 0; j < freq - 1; j++)
                        model.Add(dayVar[i, j] < dayVar[i, j + 1]);
                }

                // 4. No-conflict between selected classes ──────────────────────────────────
                var intervals = new ClassDateInterval[numClasses];
                for (int i = 0; i < numClasses; i++)
                {
                    var start = classes[i].StartDate ?? DateTime.Today.AddDays(7);
                    int freq  = frequencies[i];
                    int weeks = (int)Math.Ceiling((double)(classes[i].ExpectedLessons ?? 30) / freq);
                    intervals[i] = new ClassDateInterval
                        { ClassIndex = i, StartDate = start, EndDate = start.AddDays(weeks * 7) };
                }

                for (int i1 = 0; i1 < numClasses; i1++)
                for (int i2 = i1 + 1; i2 < numClasses; i2++)
                {
                    // Only constrain if their date ranges overlap
                    if (intervals[i1].StartDate > intervals[i2].EndDate ||
                        intervals[i2].StartDate > intervals[i1].EndDate) continue;

                    int freq1 = frequencies[i1], freq2 = frequencies[i2];
                    for (int j1 = 0; j1 < freq1; j1++)
                    for (int j2 = 0; j2 < freq2; j2++)
                    {
                        // sameSlot <=> same flat (day + time) for both sessions
                        var same = model.NewBoolVar($"same_{i1}_{j1}_{i2}_{j2}");
                        model.Add(slotVar[i1, j1] == slotVar[i2, j2]).OnlyEnforceIf(same);
                        model.Add(slotVar[i1, j1] != slotVar[i2, j2]).OnlyEnforceIf(same.Not());

                        // same slot → different teacher AND different room
                        model.Add(teacherVar[i1] != teacherVar[i2]).OnlyEnforceIf(same);
                        model.Add(roomVar[i1]    != roomVar[i2]).OnlyEnforceIf(same);
                    }
                }

                // 5. No-conflict against existing DB schedules ─────────────────────────────
                var minStart = intervals.Min(d => d.StartDate);
                var maxEnd   = intervals.Max(d => d.EndDate);

                var dbSchedules = await _dbContext.ClassSchedules
                    .Include(cs => cs.Class)
                    .Include(cs => cs.TimeSlot)
                    .Where(cs => cs.Class != null && !cs.Class.IsDeleted
                              && cs.ClassId.HasValue && !classIds.Contains(cs.ClassId.Value)
                              && cs.ScheduleDate >= minStart && cs.ScheduleDate <= maxEnd)
                    .ToListAsync();

                // Build lookup: (dayOfWeek 1-6, fixedSlotIdx) → list of (teacherId?, roomId?)
                var occupied = new Dictionary<(int day, int fi), List<(int? tId, int? rId)>>();
                foreach (var ext in dbSchedules)
                {
                    if (!ext.ScheduleDate.HasValue || ext.TimeSlot == null) continue;
                    int extDay = (int)ext.ScheduleDate.Value.DayOfWeek; // 0=Sun..6=Sat
                    int fi     = Array.FindIndex(fixedSlots, fs => fs.Start == ext.TimeSlot.StartTime);
                    if (extDay < 1 || extDay > 6 || fi < 0) continue;

                    var key = (extDay, fi);
                    if (!occupied.ContainsKey(key)) occupied[key] = new();
                    occupied[key].Add((ext.TeacherId, ext.RoomId));
                }

                for (int i = 0; i < numClasses; i++)
                for (int j = 0; j < frequencies[i]; j++)
                {
                    foreach (var kvp in occupied)
                    {
                        int extDay = kvp.Key.day;
                        int extFi  = kvp.Key.fi;

                        // Indicator: this session lands on extDay AND extFi
                        var dayMatch  = model.NewBoolVar($"dm_{i}_{j}_{extDay}");
                        var slotMatch = model.NewBoolVar($"sm_{i}_{j}_{extFi}");
                        model.Add(dayVar[i, j]       == extDay).OnlyEnforceIf(dayMatch);
                        model.Add(dayVar[i, j]       != extDay).OnlyEnforceIf(dayMatch.Not());
                        model.Add(slotIndexVar[i, j] == extFi).OnlyEnforceIf(slotMatch);
                        model.Add(slotIndexVar[i, j] != extFi).OnlyEnforceIf(slotMatch.Not());

                        // both <=> dayMatch AND slotMatch
                        var both = model.NewBoolVar($"both_{i}_{j}_{extDay}_{extFi}");
                        model.AddBoolAnd(new[] { dayMatch, slotMatch }).OnlyEnforceIf(both);
                        model.AddBoolOr(new ILiteral[] { dayMatch.Not(), slotMatch.Not() }).OnlyEnforceIf(both.Not());

                        foreach (var (tId, rId) in kvp.Value)
                        {
                            if (tId.HasValue)
                            {
                                int tIdx = teachers.FindIndex(t => t.Id == tId.Value);
                                if (tIdx >= 0) model.Add(teacherVar[i] != tIdx).OnlyEnforceIf(both);
                            }
                            if (rId.HasValue)
                            {
                                int rIdx = rooms.FindIndex(r => r.Id == rId.Value);
                                if (rIdx >= 0) model.Add(roomVar[i] != rIdx).OnlyEnforceIf(both);
                            }
                        }
                    }
                }

                // 6. Solve ─────────────────────────────────────────────────────────────────
                var solver = new CpSolver();
                solver.StringParameters = "max_time_in_seconds:30.0";
                var status = solver.Solve(model);

                if (status != CpSolverStatus.Feasible && status != CpSolverStatus.Optimal)
                    return ApiResponse<List<ClassDto>>.Fail("ERR_NO_FEASIBLE_SCHEDULE_FOUND", StatusCodes.Status409Conflict);

                // 7. Persist ───────────────────────────────────────────────────────────────
                using var transaction = await _dbContext.Database.BeginTransactionAsync();
                try
                {
                    for (int i = 0; i < numClasses; i++)
                    {
                        var entity = classes[i];
                        int tIdx   = (int)solver.Value(teacherVar[i]);
                        int rIdx   = (int)solver.Value(roomVar[i]);

                        entity.TeacherId = teachers[tIdx].Id;
                        entity.StartDate = intervals[i].StartDate;
                        entity.Status    = (int)ClassStatus.Planning;

                        int freq = frequencies[i];
                        var newWS = new List<WeeklyScheduleDto>();
                        for (int j = 0; j < freq; j++)
                        {
                            int dayVal = (int)solver.Value(dayVar[i, j]);
                            int fsVal  = (int)solver.Value(slotIndexVar[i, j]);
                            var fs     = fixedSlots[fsVal];
                            newWS.Add(new WeeklyScheduleDto
                            {
                                DayOfWeek = dayVal,
                                StartTime = fs.Start.ToString(@"hh\:mm"),
                                EndTime   = fs.End.ToString(@"hh\:mm"),
                                RoomId    = rooms[rIdx].Id
                            });
                        }

                        var saveDto = new ClassSaveDto
                        {
                            Id              = entity.Id,
                            Code            = entity.Code ?? string.Empty,
                            Name            = entity.Name ?? string.Empty,
                            Status          = entity.Status,
                            StartDate       = entity.StartDate,
                            ExpectedLessons = entity.ExpectedLessons,
                            TeacherId       = entity.TeacherId,
                            WeeklySchedules = newWS
                        };

                        if (entity.ClassSchedules?.Any() == true)
                            _dbContext.ClassSchedules.RemoveRange(entity.ClassSchedules);

                        await GenerateClassSchedulesHelperAsync(entity, saveDto, timeSlots);
                        _dbContext.Classes.Update(entity);
                    }

                    await _dbContext.SaveChangesAsync();
                    await transaction.CommitAsync();

                    var resultList = new List<ClassDto>();
                    foreach (var c in classes)
                    {
                        var reloaded = await _dbContext.Classes
                            .Include(cl => cl.Course)
                            .Include(cl => cl.Teacher)
                            .Include(cl => cl.ClassSchedules).ThenInclude(cs => cs.TimeSlot)
                            .Include(cl => cl.ClassSchedules).ThenInclude(cs => cs.Room)
                            .FirstOrDefaultAsync(cl => cl.Id == c.Id);
                        if (reloaded != null) resultList.Add(MapToDto(reloaded));
                    }

                    return ApiResponse<List<ClassDto>>.Ok(resultList, "AUTO_SCHEDULING_COMPLETED");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    throw new Exception("Error writing auto scheduling results: " + ex.Message, ex);
                }
            }
            catch (Exception ex)
            {
                return ApiResponse<List<ClassDto>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        // ================= PRIVATE HELPERS =================

        private class ProposedScheduleTemp
        {
            public int LessonNo { get; set; }
            public DateTime Date { get; set; }
            public TimeSpan StartTime { get; set; }
            public TimeSpan EndTime { get; set; }
            public int? RoomId { get; set; }
        }

        private class ClassDateInterval
        {
            public int ClassIndex { get; set; }
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
        }

        private async Task GenerateClassSchedulesHelperAsync(Class entity, ClassSaveDto dto, List<TimeSlot> dbTimeSlots)
        {
            if (dto.WeeklySchedules == null || !dto.WeeklySchedules.Any() || !dto.StartDate.HasValue || !dto.ExpectedLessons.HasValue || dto.ExpectedLessons.Value <= 0)
            {
                return;
            }

            entity.ScheduleDisplay = string.Join(", ", dto.WeeklySchedules
                .OrderBy(w => w.DayOfWeek)
                .Select(w => $"{GetDayOfWeekName(w.DayOfWeek)} {w.StartTime}-{w.EndTime}"));

            var jsonOptions = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            };
            entity.WeeklySchedulesJson = System.Text.Json.JsonSerializer.Serialize(dto.WeeklySchedules, jsonOptions);
            entity.ExpectedLessons = dto.ExpectedLessons;

            var currentDate = dto.StartDate.Value;
            int lessonNo = 1;
            var weeklySchedules = dto.WeeklySchedules.OrderBy(w => w.DayOfWeek).ToList();

            while (lessonNo <= dto.ExpectedLessons.Value)
            {
                var match = weeklySchedules.FirstOrDefault(w => (int)currentDate.DayOfWeek == w.DayOfWeek);
                if (match != null)
                {
                    var startSpan = TimeSpan.Parse(match.StartTime);
                    var endSpan = TimeSpan.Parse(match.EndTime);

                    var timeSlot = dbTimeSlots.FirstOrDefault(ts => ts.StartTime == startSpan && ts.EndTime == endSpan);
                    if (timeSlot == null)
                    {
                        timeSlot = new TimeSlot
                        {
                            Code = $"TS_{match.StartTime.Replace(":", "")}_{match.EndTime.Replace(":", "")}",
                            Name = $"{match.StartTime} - {match.EndTime}",
                            StartTime = startSpan,
                            EndTime = endSpan
                        };
                        _dbContext.TimeSlots.Add(timeSlot);
                        await _dbContext.SaveChangesAsync();
                        dbTimeSlots.Add(timeSlot); // Add to cache list
                    }

                    entity.ClassSchedules.Add(new ClassSchedule
                    {
                        LessonNo = lessonNo,
                        ScheduleDate = currentDate,
                        SlotId = timeSlot.Id,
                        RoomId = match.RoomId,
                        TeacherId = dto.TeacherId,
                        Status = (int)ClassScheduleStatus.Scheduled,
                        Code = $"SCH_{entity.Code}_{lessonNo}",
                        Name = $"Buổi học {lessonNo} - {entity.Name}"
                    });
                    lessonNo++;
                }
                currentDate = currentDate.AddDays(1);
            }

            if (entity.ClassSchedules.Any())
            {
                entity.EndDate = entity.ClassSchedules.Last().ScheduleDate;
            }
        }

        private static string GetDayOfWeekName(int day)
        {
            return day switch
            {
                0 => "CN",
                1 => "T2",
                2 => "T3",
                3 => "T4",
                4 => "T5",
                5 => "T6",
                6 => "T7",
                _ => ""
            };
        }

        private static ClassDto MapToDto(Class entity) => new()
        {
            Id = entity.Id,
            Code = entity.Code ?? string.Empty,
            Name = entity.Name ?? string.Empty,
            Status = entity.Status,
            StatusName = ((ClassStatus)entity.Status).GetStringValue(),
            Description = entity.Description,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            CourseId = entity.CourseId,
            CourseName = entity.Course?.Name,
            TeacherId = entity.TeacherId,
            TeacherName = entity.Teacher?.Name,
            TeacherAvatar = entity.Teacher?.Avatar,
            ScheduleDisplay = entity.ScheduleDisplay,
            StudentCount = entity.StudentClasses.Count,
            ExpectedLessons = entity.ExpectedLessons,
            WeeklySchedulesJson = entity.WeeklySchedulesJson,
            AutoRefund = entity.AutoRefund,
            Schedules = entity.ClassSchedules?.Select(cs => new ClassScheduleDto
            {
                Id = cs.Id,
                ClassId = cs.ClassId,
                ClassCode = cs.Class?.Code,
                ClassName = cs.Class?.Name,
                LessonNo = cs.LessonNo,
                ScheduleDate = cs.ScheduleDate,
                SlotId = cs.SlotId,
                SlotName = cs.TimeSlot?.Name,
                StartTime = cs.TimeSlot?.StartTime.ToString(@"hh\:mm"),
                EndTime = cs.TimeSlot?.EndTime.ToString(@"hh\:mm"),
                RoomId = cs.RoomId,
                RoomName = cs.Room?.Name,
                TeacherId = cs.TeacherId,
                TeacherName = cs.Teacher?.Name,
                TeacherAvatar = cs.Teacher?.Avatar,
                Status = cs.Status,
                Note = cs.Note
            }).OrderBy(cs => cs.LessonNo).ToList() ?? new List<ClassScheduleDto>(),
            StudentClasses = entity.StudentClasses?.Select(sc => new ClassStudentDto
            {
                Id = sc.Id,
                StudentId = sc.StudentId,
                Student = sc.Student != null ? new PRN232_be.DTO.Student.StudentDto
                {
                    Id = sc.Student.Id,
                    Code = sc.Student.Code ?? string.Empty,
                    Name = sc.Student.Name ?? string.Empty,
                    Email = sc.Student.Email,
                    Phone = sc.Student.Phone,
                    Avatar = sc.Student.Avatar
                } : null
            }).ToList() ?? new List<ClassStudentDto>()
        };
    }
}
