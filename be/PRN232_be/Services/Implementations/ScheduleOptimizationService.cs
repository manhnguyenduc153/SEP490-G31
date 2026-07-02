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

        public async Task<ApiResponse<List<ClassDto>>> AutoScheduleAsync(List<int> classIds, AutoScheduleConstraintDto constraints)
        {
            try
            {
                if (classIds == null || !classIds.Any())
                    return ApiResponse<List<ClassDto>>.Fail("ERR_NO_CLASSES_SELECTED", StatusCodes.Status400BadRequest);

                // Validate constraints
                constraints ??= new AutoScheduleConstraintDto();
                constraints.SessionsPerWeek = Math.Clamp(constraints.SessionsPerWeek, 1, 3);
                if (constraints.TimePreferences == null || !constraints.TimePreferences.Any())
                    constraints.TimePreferences = new List<string> { "morning", "afternoon", "evening" };

                // ── 1. Load all selected classes ────────────────────────────────────────
                var allClasses = await _dbContext.Classes
                    .Include(c => c.Course)
                    .Include(c => c.Teacher)
                    .Include(c => c.ClassSchedules)
                    .Include(c => c.StudentClasses)
                    .Where(c => classIds.Contains(c.Id) && !c.IsDeleted)
                    .ToListAsync();

                if (!allClasses.Any())
                    return ApiResponse<List<ClassDto>>.Fail("ERR_CLASSES_NOT_FOUND", StatusCodes.Status404NotFound);

                // Separate: classes to schedule vs classes already scheduled (skip but use for conflict)
                var jsonOpts = new System.Text.Json.JsonSerializerOptions
                    { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };

                bool HasExistingSchedule(Class c)
                {
                    if (string.IsNullOrWhiteSpace(c.WeeklySchedulesJson)) return false;
                    try
                    {
                        var list = System.Text.Json.JsonSerializer.Deserialize<List<WeeklyScheduleDto>>(c.WeeklySchedulesJson, jsonOpts);
                        return list != null && list.Count > 0;
                    }
                    catch { return false; }
                }

                var classesToSchedule = allClasses.Where(c => !HasExistingSchedule(c)).ToList();
                var classesAlreadyScheduled = allClasses.Where(c => HasExistingSchedule(c)).ToList();

                if (!classesToSchedule.Any())
                    return ApiResponse<List<ClassDto>>.Fail("ERR_ALL_CLASSES_ALREADY_SCHEDULED", StatusCodes.Status400BadRequest);

                // ── 2. Validate per-class requirements ──────────────────────────────────
                foreach (var c in classesToSchedule)
                {
                    if (c.CourseId == null)
                        return ApiResponse<List<ClassDto>>.Fail($"ERR_CLASS_NO_COURSE_{c.Code}", StatusCodes.Status400BadRequest);
                    if (c.StudentClasses == null || !c.StudentClasses.Any())
                        return ApiResponse<List<ClassDto>>.Fail($"ERR_CLASS_NO_STUDENTS_{c.Code}", StatusCodes.Status400BadRequest);
                }

                // ── 3. Load resources ───────────────────────────────────────────────────
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
                    return ApiResponse<List<ClassDto>>.Fail("ERR_NO_ACTIVE_TEACHERS", StatusCodes.Status400BadRequest);
                if (!rooms.Any())
                    return ApiResponse<List<ClassDto>>.Fail("ERR_NO_ACTIVE_ROOMS", StatusCodes.Status400BadRequest);
                if (!timeSlots.Any())
                    return ApiResponse<List<ClassDto>>.Fail("ERR_NO_TIMESLOTS", StatusCodes.Status400BadRequest);

                // Check room capacities for each class to be scheduled
                foreach (var c in classesToSchedule)
                {
                    int studentCount = c.StudentClasses?.Count ?? 0;
                    if (!rooms.Any(r => (r.Capacity ?? int.MaxValue) >= studentCount))
                    {
                        return ApiResponse<List<ClassDto>>.Fail($"ERR_CLASS_STUDENTS_EXCEED_ROOM_CAPACITY_{c.Code}", StatusCodes.Status400BadRequest);
                    }
                }

                // ── 4. Fixed 5 slots ────────────────────────────────────────────────────
                var fixedSlots = FixedTimeSlot.All;
                int numFixed = fixedSlots.Length;

                // Map time preference buckets → allowed slot indices
                var slotMap = new Dictionary<string, int[]>
                {
                    { "morning",   new[] { 0, 1 } },
                    { "afternoon", new[] { 2, 3 } },
                    { "evening",   new[] { 4 }    }
                };
                var allowedSlotIndices = constraints.TimePreferences
                    .Where(p => slotMap.ContainsKey(p))
                    .SelectMany(p => slotMap[p])
                    .Distinct()
                    .OrderBy(x => x)
                    .ToArray();
                if (!allowedSlotIndices.Any())
                    allowedSlotIndices = new[] { 0, 1, 2, 3, 4 };

                // Allowed days (DayOfWeek)
                var allowedDays = constraints.AllowWeekend
                    ? new[] { 0, 1, 2, 3, 4, 5, 6 }
                    : new[] { 1, 2, 3, 4, 5 };

                int numClasses  = classesToSchedule.Count;
                int numTeachers = teachers.Count;
                int numRooms    = rooms.Count;
                int freq        = constraints.SessionsPerWeek; // same for all unscheduled classes

                // ── 5. Build CP-SAT model ──────────────────────────────────────────────
                var model = new CpModel();

                var teacherVar   = new IntVar[numClasses];
                var roomVar      = new IntVar[numClasses];
                var dayVar       = new IntVar[numClasses, freq];
                var slotIndexVar = new IntVar[numClasses, freq];
                var slotVar      = new IntVar[numClasses, freq]; // flat = day*numFixed + slotIndex

                for (int i = 0; i < numClasses; i++)
                {
                    var classToSchedule = classesToSchedule[i];
                    teacherVar[i] = model.NewIntVar(0, numTeachers - 1, $"t_{i}");
                    roomVar[i]    = model.NewIntVar(0, numRooms - 1,    $"r_{i}");

                    // Pin teacher if already assigned and still active
                    if (classToSchedule.TeacherId.HasValue)
                    {
                        int pinTeacherId = classToSchedule.TeacherId.Value;
                        int tIdx = teachers.FindIndex(t => t.Id == pinTeacherId);
                        if (tIdx >= 0) model.Add(teacherVar[i] == tIdx);
                    }

                    // Room capacity check: room capacity must be >= class student count
                    int studentCount = classToSchedule.StudentClasses?.Count ?? 0;
                    for (int rIdx = 0; rIdx < numRooms; rIdx++)
                    {
                        if ((rooms[rIdx].Capacity ?? int.MaxValue) < studentCount)
                        {
                            model.Add(roomVar[i] != rIdx);
                        }
                    }

                    for (int j = 0; j < freq; j++)
                    {
                        // Day variable — restricted to allowedDays
                        dayVar[i, j]       = model.NewIntVar(allowedDays.Min(), allowedDays.Max(), $"day_{i}_{j}");
                        slotIndexVar[i, j] = model.NewIntVar(allowedSlotIndices.Min(), allowedSlotIndices.Max(), $"fs_{i}_{j}");
                        slotVar[i, j]      = model.NewIntVar(0, 7 * numFixed - 1, $"flat_{i}_{j}");
                        model.Add(slotVar[i, j] == dayVar[i, j] * numFixed + slotIndexVar[i, j]);

                        // Restrict dayVar to allowedDays (BoolOr over equality literals)
                        if (allowedDays.Length < allowedDays.Max() - allowedDays.Min() + 1)
                        {
                            var dayLiterals = allowedDays.Select(d =>
                            {
                                var b = model.NewBoolVar($"dayOk_{i}_{j}_{d}");
                                model.Add(dayVar[i, j] == d).OnlyEnforceIf(b);
                                model.Add(dayVar[i, j] != d).OnlyEnforceIf(b.Not());
                                return (ILiteral)b;
                            }).ToArray();
                            model.AddBoolOr(dayLiterals);
                        }

                        // Restrict slotIndexVar to allowedSlotIndices
                        if (allowedSlotIndices.Length < allowedSlotIndices.Max() - allowedSlotIndices.Min() + 1)
                        {
                            var slotLiterals = allowedSlotIndices.Select(s =>
                            {
                                var b = model.NewBoolVar($"slotOk_{i}_{j}_{s}");
                                model.Add(slotIndexVar[i, j] == s).OnlyEnforceIf(b);
                                model.Add(slotIndexVar[i, j] != s).OnlyEnforceIf(b.Not());
                                return (ILiteral)b;
                            }).ToArray();
                            model.AddBoolOr(slotLiterals);
                        }
                    }

                    // Sessions of the same class: ordered days + gap constraint
                    for (int j = 0; j < freq - 1; j++)
                    {
                        if (constraints.AllowConsecutiveDays)
                            model.Add(dayVar[i, j + 1] > dayVar[i, j]);        // just different, ascending
                        else
                            model.Add(dayVar[i, j + 1] >= dayVar[i, j] + 2);   // at least 1 day gap
                    }
                }

                // ── 6. No-conflict between classes being scheduled ─────────────────────
                var intervals = new ClassDateInterval[numClasses];
                for (int i = 0; i < numClasses; i++)
                {
                    var start = classesToSchedule[i].StartDate ?? DateTime.Today.AddDays(7);
                    int weeks = (int)Math.Ceiling((double)(classesToSchedule[i].ExpectedLessons ?? 30) / freq);
                    intervals[i] = new ClassDateInterval
                        { ClassIndex = i, StartDate = start, EndDate = start.AddDays(weeks * 7) };
                }

                for (int i1 = 0; i1 < numClasses; i1++)
                for (int i2 = i1 + 1; i2 < numClasses; i2++)
                {
                    if (intervals[i1].StartDate > intervals[i2].EndDate ||
                        intervals[i2].StartDate > intervals[i1].EndDate) continue;

                    for (int j1 = 0; j1 < freq; j1++)
                    for (int j2 = 0; j2 < freq; j2++)
                    {
                        var same = model.NewBoolVar($"same_{i1}_{j1}_{i2}_{j2}");
                        model.Add(slotVar[i1, j1] == slotVar[i2, j2]).OnlyEnforceIf(same);
                        model.Add(slotVar[i1, j1] != slotVar[i2, j2]).OnlyEnforceIf(same.Not());
                        model.Add(teacherVar[i1] != teacherVar[i2]).OnlyEnforceIf(same);
                        model.Add(roomVar[i1]    != roomVar[i2]).OnlyEnforceIf(same);
                    }
                }

                // ── 7. No-conflict against EXISTING DB schedules ───────────────────────
                var minStart = intervals.Min(d => d.StartDate);
                var maxEnd   = intervals.Max(d => d.EndDate);

                // Include already-scheduled classes from this batch in the conflict scope
                var skipIds = new HashSet<int>(classesToSchedule.Select(c => c.Id));

                var dbSchedules = await _dbContext.ClassSchedules
                    .Include(cs => cs.Class)
                    .Include(cs => cs.TimeSlot)
                    .Where(cs => cs.Class != null && !cs.Class.IsDeleted
                              && cs.ClassId.HasValue && !skipIds.Contains(cs.ClassId.Value)
                              && cs.ScheduleDate >= minStart && cs.ScheduleDate <= maxEnd)
                    .ToListAsync();

                // Group by (dayOfWeek, fixedSlotIdx) → list of (teacherId?, roomId?)
                var occupied = new Dictionary<(int day, int fi), List<(int? tId, int? rId)>>();
                foreach (var ext in dbSchedules)
                {
                    if (!ext.ScheduleDate.HasValue || ext.TimeSlot == null) continue;
                    int extDay = (int)ext.ScheduleDate.Value.DayOfWeek;
                    int fi     = Array.FindIndex(fixedSlots, fs => fs.Start == ext.TimeSlot.StartTime);
                    if (extDay < 0 || extDay > 6 || fi < 0) continue;
                    var key = (extDay, fi);
                    if (!occupied.ContainsKey(key)) occupied[key] = new();
                    occupied[key].Add((ext.TeacherId, ext.RoomId));
                }

                for (int i = 0; i < numClasses; i++)
                for (int j = 0; j < freq; j++)
                {
                    foreach (var kvp in occupied)
                    {
                        int extDay = kvp.Key.day;
                        int extFi  = kvp.Key.fi;

                        var dayMatch  = model.NewBoolVar($"dm_{i}_{j}_{extDay}");
                        var slotMatch = model.NewBoolVar($"sm_{i}_{j}_{extFi}");
                        model.Add(dayVar[i, j]       == extDay).OnlyEnforceIf(dayMatch);
                        model.Add(dayVar[i, j]       != extDay).OnlyEnforceIf(dayMatch.Not());
                        model.Add(slotIndexVar[i, j] == extFi).OnlyEnforceIf(slotMatch);
                        model.Add(slotIndexVar[i, j] != extFi).OnlyEnforceIf(slotMatch.Not());

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

                // ── 8. Load-balancing objective ────────────────────────────────────────
                // For each allowed slot index s, count how many classes use it in at least one session.
                // Minimize (maxCount - minCount) to spread classes evenly across time buckets.
                IntVar? spread = null;
                if (allowedSlotIndices.Length > 1 && numClasses > 1)
                {
                    var countAtSlot = new IntVar[numFixed];
                    for (int s = 0; s < numFixed; s++)
                    {
                        if (!allowedSlotIndices.Contains(s))
                        {
                            countAtSlot[s] = model.NewConstant(0);
                            continue;
                        }
                        countAtSlot[s] = model.NewIntVar(0, numClasses, $"cnt_{s}");

                        // classUsesSlot[i][s] = 1 if class i has any session at slot s
                        var perClass = new List<IntVar>();
                        for (int i = 0; i < numClasses; i++)
                        {
                            var uses = model.NewBoolVar($"uses_{i}_{s}");
                            var sessionBools = new List<BoolVar>();
                            for (int j = 0; j < freq; j++)
                            {
                                var b = model.NewBoolVar($"sb_{i}_{j}_{s}");
                                model.Add(slotIndexVar[i, j] == s).OnlyEnforceIf(b);
                                model.Add(slotIndexVar[i, j] != s).OnlyEnforceIf(b.Not());
                                sessionBools.Add(b);
                            }
                            // uses = OR(sessionBools)
                            model.AddBoolOr(sessionBools.Cast<ILiteral>().ToArray()).OnlyEnforceIf(uses);
                            model.AddBoolAnd(sessionBools.Select(b => (ILiteral)b.Not()).ToArray()).OnlyEnforceIf(uses.Not());
                            perClass.Add(uses); // BoolVar is IntVar subtype via LinearExpr
                        }
                        model.Add(countAtSlot[s] == LinearExpr.Sum(perClass.ToArray()));
                    }

                    var activeCounts = allowedSlotIndices.Select(s => countAtSlot[s]).ToArray();
                    var maxCount = model.NewIntVar(0, numClasses, "maxCnt");
                    var minCount = model.NewIntVar(0, numClasses, "minCnt");
                    model.AddMaxEquality(maxCount, activeCounts);
                    model.AddMinEquality(minCount, activeCounts);
                    spread = model.NewIntVar(0, numClasses, "spread");
                    model.Add(spread == maxCount - minCount);
                }

                IntVar? teacherSpread = null;
                if (numTeachers > 1 && numClasses > 1)
                {
                    var classesCountForTeacher = new IntVar[numTeachers];
                    for (int t = 0; t < numTeachers; t++)
                    {
                        classesCountForTeacher[t] = model.NewIntVar(0, numClasses, $"tCount_{t}");
                        var assignedToTeacher = new List<IntVar>();
                        for (int i = 0; i < numClasses; i++)
                        {
                            var assigned = model.NewBoolVar($"assigned_{i}_{t}");
                            model.Add(teacherVar[i] == t).OnlyEnforceIf(assigned);
                            model.Add(teacherVar[i] != t).OnlyEnforceIf(assigned.Not());
                            assignedToTeacher.Add(assigned);
                        }
                        model.Add(classesCountForTeacher[t] == LinearExpr.Sum(assignedToTeacher.ToArray()));
                    }

                    var maxTeacherClasses = model.NewIntVar(0, numClasses, "maxTeacherClasses");
                    var minTeacherClasses = model.NewIntVar(0, numClasses, "minTeacherClasses");
                    model.AddMaxEquality(maxTeacherClasses, classesCountForTeacher);
                    model.AddMinEquality(minTeacherClasses, classesCountForTeacher);
                    teacherSpread = model.NewIntVar(0, numClasses, "teacherSpread");
                    model.Add(teacherSpread == maxTeacherClasses - minTeacherClasses);
                }

                // Combine objectives
                var objectiveExpressions = new List<IntVar>();
                if (spread != null)
                {
                    objectiveExpressions.Add(spread!);
                }
                if (teacherSpread != null)
                {
                    objectiveExpressions.Add(teacherSpread!);
                }

                if (objectiveExpressions.Any())
                {
                    if (objectiveExpressions.Count == 1)
                    {
                        model.Minimize(objectiveExpressions[0]);
                    }
                    else
                    {
                        model.Minimize(LinearExpr.Sum(objectiveExpressions.ToArray()));
                    }
                }

                // ── 9. Solve ────────────────────────────────────────────────────────────
                var solver = new CpSolver();
                solver.StringParameters = "max_time_in_seconds:30.0";
                var status = solver.Solve(model);

                if (status != CpSolverStatus.Feasible && status != CpSolverStatus.Optimal)
                    return ApiResponse<List<ClassDto>>.Fail("ERR_NO_FEASIBLE_SCHEDULE_FOUND", StatusCodes.Status409Conflict);

                // ── 10. Persist ─────────────────────────────────────────────────────────
                using var transaction = await _dbContext.Database.BeginTransactionAsync();
                try
                {
                    for (int i = 0; i < numClasses; i++)
                    {
                        var entity = classesToSchedule[i];
                        int tIdx   = (int)solver.Value(teacherVar[i]);
                        int rIdx   = (int)solver.Value(roomVar[i]);

                        entity.TeacherId = teachers[tIdx].Id;
                        entity.StartDate = intervals[i].StartDate;
                        entity.Status    = (int)ClassStatus.Planning;

                        var newWS = new List<WeeklyScheduleDto>();
                        for (int j = 0; j < freq; j++)
                        {
                            int dayVal = (int)solver.Value(dayVar[i, j]);
                            int fsVal  = (int)solver.Value(slotIndexVar[i, j]);
                            var fs     = fixedSlots[fsVal];
                            newWS.Add(new WeeklyScheduleDto
                            {
                                DayOfWeek = dayVal,
                                StartTime = fs.StartStr,
                                EndTime   = fs.EndStr,
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

                    // Return all classes in the original selection (scheduled + already-had-schedule)
                    var resultList = new List<ClassDto>();
                    foreach (var c in allClasses)
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
