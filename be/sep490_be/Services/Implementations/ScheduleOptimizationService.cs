using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Google.OrTools.Sat;
using System.Text.Json;
using sep490_be.DTO;
using sep490_be.DTO.Class;
using sep490_be.DTO.Semester;
using sep490_be.DTO.Student;
using sep490_be.DTO.Teacher;
using sep490_be.Models;
using sep490_be.Services.Interfaces;
using sep490_be.Enums;

namespace sep490_be.Services.Implementations
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

                weeklySchedules = weeklySchedules
                    .Where(w => TimeSpan.TryParse(w.StartTime, out _) &&
                                TimeSpan.TryParse(w.EndTime, out _))
                    .ToList();
                if (!weeklySchedules.Any())
                {
                    return ApiResponse<ConflictCheckResultDto>.Ok(result, "NO_PROPOSED_SCHEDULES_GENERATED");
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
                if (!teachers.Any())
                    return ApiResponse<List<ClassDto>>.Fail("ERR_NO_ACTIVE_TEACHERS", StatusCodes.Status400BadRequest);
                if (!rooms.Any())
                    return ApiResponse<List<ClassDto>>.Fail("ERR_NO_ACTIVE_ROOMS", StatusCodes.Status400BadRequest);

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
                var roomPenaltyVar = new IntVar[numClasses];
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

                    // Room capacity penalty: minimize (roomCapacity - classSize)
                    roomPenaltyVar[i] = model.NewIntVar(0, 9999, $"roomPenalty_{i}");
                    long[] roomPenaltiesForClass = rooms.Select(r => {
                        long cap = r.Capacity ?? 999;
                        return cap >= studentCount ? (cap - studentCount) : 9999;
                    }).ToArray();
                    model.AddElement(roomVar[i], roomPenaltiesForClass, roomPenaltyVar[i]);

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

                    // Constraint: The slot index must be the same for all sessions of a class
                    for (int j = 1; j < freq; j++)
                    {
                        model.Add(slotIndexVar[i, j] == slotIndexVar[i, 0]);
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

                IntVar? roomSpread = null;
                if (numRooms > 1 && numClasses > 1)
                {
                    var classesCountForRoom = new IntVar[numRooms];
                    for (int r = 0; r < numRooms; r++)
                    {
                        classesCountForRoom[r] = model.NewIntVar(0, numClasses, $"rCount_{r}");
                        var assignedToRoom = new List<IntVar>();
                        for (int i = 0; i < numClasses; i++)
                        {
                            var assigned = model.NewBoolVar($"assignedRoom_{i}_{r}");
                            model.Add(roomVar[i] == r).OnlyEnforceIf(assigned);
                            model.Add(roomVar[i] != r).OnlyEnforceIf(assigned.Not());
                            assignedToRoom.Add(assigned);
                        }
                        model.Add(classesCountForRoom[r] == LinearExpr.Sum(assignedToRoom.ToArray()));
                    }

                    var maxRoomClasses = model.NewIntVar(0, numClasses, "maxRoomClasses");
                    var minRoomClasses = model.NewIntVar(0, numClasses, "minRoomClasses");
                    model.AddMaxEquality(maxRoomClasses, classesCountForRoom);
                    model.AddMinEquality(minRoomClasses, classesCountForRoom);
                    roomSpread = model.NewIntVar(0, numClasses, "roomSpread");
                    model.Add(roomSpread == maxRoomClasses - minRoomClasses);
                }

                // Combine objectives
                var objectiveExpressions = new List<IntVar>();
                if (spread != null)
                {
                    objectiveExpressions.Add(spread!);
                }
                if (teacherSpread != null)
                {
                    // Scale teacherSpread by 100 to ensure workload balance is the highest priority
                    var weightedTeacherSpread = model.NewIntVar(0, numClasses * 100, "weightedTeacherSpread");
                    model.Add(weightedTeacherSpread == teacherSpread * 100);
                    objectiveExpressions.Add(weightedTeacherSpread);
                }
                if (roomSpread != null)
                {
                    // Scale roomSpread by 50 to ensure room utilization balance is a high priority
                    var weightedRoomSpread = model.NewIntVar(0, numClasses * 50, "weightedRoomSpread");
                    model.Add(weightedRoomSpread == roomSpread * 50);
                    objectiveExpressions.Add(weightedRoomSpread);
                }

                // Add room capacity penalty (minimize difference between capacity and size)
                var totalRoomPenalty = model.NewIntVar(0, 999999, "totalRoomPenalty");
                model.Add(totalRoomPenalty == LinearExpr.Sum(roomPenaltyVar));
                var weightedRoomPenalty = model.NewIntVar(0, 999999 * 5, "weightedRoomPenalty");
                model.Add(weightedRoomPenalty == totalRoomPenalty * 5);
                objectiveExpressions.Add(weightedRoomPenalty);

                // ── Optimization: Minimize active teaching days & gaps for teachers ───────────
                if (numClasses > 1)
                {
                    // Pre-calculate isTeacher[i, t]
                    var isTeacher = new BoolVar[numClasses, numTeachers];
                    for (int i = 0; i < numClasses; i++)
                    {
                        for (int t = 0; t < numTeachers; t++)
                        {
                            isTeacher[i, t] = model.NewBoolVar($"is_t_opt_{i}_{t}");
                            model.Add(teacherVar[i] == t).OnlyEnforceIf(isTeacher[i, t]);
                            model.Add(teacherVar[i] != t).OnlyEnforceIf(isTeacher[i, t].Not());
                        }
                    }

                    // Pre-calculate isDay[i, j, d] for d in allowedDays
                    var isDay = new Dictionary<(int classIdx, int sessionIdx, int day), BoolVar>();
                    foreach (int d in allowedDays)
                    {
                        for (int i = 0; i < numClasses; i++)
                        {
                            for (int j = 0; j < freq; j++)
                            {
                                var dayBool = model.NewBoolVar($"is_d_opt_{i}_{j}_{d}");
                                model.Add(dayVar[i, j] == d).OnlyEnforceIf(dayBool);
                                model.Add(dayVar[i, j] != d).OnlyEnforceIf(dayBool.Not());
                                isDay[(i, j, d)] = dayBool;
                            }
                        }
                    }

                    // Define teacherTeachesOnDay[t, d] & gapPenalties
                    var teacherTeachesOnDay = new List<BoolVar>();
                    var gapPenalties = new List<(BoolVar Var, int Weight)>();

                    for (int t = 0; t < numTeachers; t++)
                    {
                        foreach (int d in allowedDays)
                        {
                            var tTeachesOnD = model.NewBoolVar($"t_teaches_opt_{t}_{d}");
                            var activeSessions = new List<IntVar>();

                            // teachesAtSlot[s] represents if teacher t teaches on day d at slot s
                            var teachesAtSlot = new BoolVar[numFixed];
                            for (int s = 0; s < numFixed; s++)
                            {
                                teachesAtSlot[s] = model.NewBoolVar($"teaches_t_{t}_d_{d}_s_{s}");
                                if (!allowedSlotIndices.Contains(s))
                                {
                                    model.Add(teachesAtSlot[s] == 0);
                                    continue;
                                }

                                var sessionsAtSlot = new List<BoolVar>();
                                for (int i = 0; i < numClasses; i++)
                                {
                                    for (int j = 0; j < freq; j++)
                                    {
                                        var isBoth = model.NewBoolVar($"is_both_opt_{i}_{j}_{t}_{d}_{s}");
                                        var isSlot = model.NewBoolVar($"is_slot_opt_{i}_{j}_{s}");
                                        model.Add(slotIndexVar[i, j] == s).OnlyEnforceIf(isSlot);
                                        model.Add(slotIndexVar[i, j] != s).OnlyEnforceIf(isSlot.Not());

                                        model.AddBoolAnd(new[] { isTeacher[i, t], isDay[(i, j, d)], isSlot }).OnlyEnforceIf(isBoth);
                                        model.AddBoolOr(new ILiteral[] { isTeacher[i, t].Not(), isDay[(i, j, d)].Not(), isSlot.Not() }).OnlyEnforceIf(isBoth.Not());
                                        sessionsAtSlot.Add(isBoth);
                                    }
                                }

                                model.AddBoolOr(sessionsAtSlot.Cast<ILiteral>().ToArray()).OnlyEnforceIf(teachesAtSlot[s]);
                                model.AddBoolAnd(sessionsAtSlot.Select(b => (ILiteral)b.Not()).ToArray()).OnlyEnforceIf(teachesAtSlot[s].Not());
                            }

                            for (int i = 0; i < numClasses; i++)
                            {
                                for (int j = 0; j < freq; j++)
                                {
                                    var isBoth = model.NewBoolVar($"is_both_opt_{i}_{j}_{t}_{d}");
                                    model.AddBoolAnd(new[] { isTeacher[i, t], isDay[(i, j, d)] }).OnlyEnforceIf(isBoth);
                                    model.AddBoolOr(new ILiteral[] { isTeacher[i, t].Not(), isDay[(i, j, d)].Not() }).OnlyEnforceIf(isBoth.Not());
                                    activeSessions.Add(isBoth);
                                }
                            }

                            var sumExpr = LinearExpr.Sum(activeSessions.ToArray());
                            model.Add(sumExpr >= 1).OnlyEnforceIf(tTeachesOnD);
                            model.Add(sumExpr == 0).OnlyEnforceIf(tTeachesOnD.Not());
                            teacherTeachesOnDay.Add(tTeachesOnD);

                            // Calculate gaps (distance >= 3)
                            // Slot 0 and Slot 4 (distance 4) -> penalty 60
                            var gap04 = model.NewBoolVar($"gap04_{t}_{d}");
                            model.AddBoolAnd(new[] { teachesAtSlot[0], teachesAtSlot[4] }).OnlyEnforceIf(gap04);
                            model.AddBoolOr(new ILiteral[] { teachesAtSlot[0].Not(), teachesAtSlot[4].Not() }).OnlyEnforceIf(gap04.Not());
                            gapPenalties.Add((gap04, 60));

                            // Slot 0 and Slot 3 (distance 3) -> penalty 30
                            var gap03 = model.NewBoolVar($"gap03_{t}_{d}");
                            model.AddBoolAnd(new[] { teachesAtSlot[0], teachesAtSlot[3] }).OnlyEnforceIf(gap03);
                            model.AddBoolOr(new ILiteral[] { teachesAtSlot[0].Not(), teachesAtSlot[3].Not() }).OnlyEnforceIf(gap03.Not());
                            gapPenalties.Add((gap03, 30));

                            // Slot 1 and Slot 4 (distance 3) -> penalty 30
                            var gap14 = model.NewBoolVar($"gap14_{t}_{d}");
                            model.AddBoolAnd(new[] { teachesAtSlot[1], teachesAtSlot[4] }).OnlyEnforceIf(gap14);
                            model.AddBoolOr(new ILiteral[] { teachesAtSlot[1].Not(), teachesAtSlot[4].Not() }).OnlyEnforceIf(gap14.Not());
                            gapPenalties.Add((gap14, 30));
                        }
                    }

                    var totalTeacherDays = model.NewIntVar(0, numTeachers * allowedDays.Length, "totalTeacherDays");
                    model.Add(totalTeacherDays == LinearExpr.Sum(teacherTeachesOnDay.Cast<IntVar>().ToArray()));

                    var weightedTeacherDays = model.NewIntVar(0, numTeachers * allowedDays.Length * 10, "weightedTeacherDays");
                    model.Add(weightedTeacherDays == totalTeacherDays * 10);
                    objectiveExpressions.Add(weightedTeacherDays);

                    // Add totalGapPenalty to objectives
                    if (gapPenalties.Any())
                    {
                        var totalGapPenalty = model.NewIntVar(0, 999999, "totalGapPenalty");
                        var gapExprs = gapPenalties.Select(gp => gp.Var * gp.Weight).ToArray();
                        model.Add(totalGapPenalty == LinearExpr.Sum(gapExprs));
                        objectiveExpressions.Add(totalGapPenalty);
                    }
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

                        await GenerateClassSchedulesHelperAsync(entity, saveDto);
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

        private class DraftClass
        {
            public int CourseId { get; set; }
            public string CourseCode { get; set; } = string.Empty;
            public string CourseName { get; set; } = string.Empty;
            public int Size => StudentIds.Count;
            public List<int> StudentIds { get; set; } = new List<int>();
            public string PreferredSlotBucket { get; set; } = "evening"; // morning, afternoon, evening
            public int ExpectedLessons { get; set; } = 30;
            public int EnrollType { get; set; } = 0; // 0 = Offline, 1 = Online
        }

        private List<DraftClass> GroupStudentsIntoDraftClasses(
            List<StudentRegistration> registrations, 
            int maxClassSize, 
            int minClassSize,
            bool[] allowedBuckets)
        {
            var draftClasses = new List<DraftClass>();

            // Ensure valid bounds
            if (minClassSize <= 0) minClassSize = 1;
            if (maxClassSize < minClassSize) maxClassSize = minClassSize;
            if (allowedBuckets == null || allowedBuckets.Length < 3)
            {
                allowedBuckets = new[] { true, true, true };
            }

            // Group by Course and EnrollType to separate Online/Offline students into distinct classes
            var byCourse = registrations.GroupBy(r => new { r.CourseId, r.EnrollType });
            foreach (var courseGroup in byCourse)
            {
                var course = courseGroup.First().Course;
                if (course == null) continue;
                int groupEnrollType = courseGroup.Key.EnrollType;

                var expectedLessons = course.Duration ?? 30;
                var students = courseGroup.ToList();
                int N = students.Count;
                if (N < minClassSize) continue;

                // We can form at most M classes
                int M = N / minClassSize;
                if (M == 0) continue;

                var model = new CpModel();

                // Variables
                var x = new BoolVar[N, M];
                var active = new BoolVar[M];
                var isSlot = new BoolVar[M, 3]; // 0: morning, 1: afternoon, 2: evening

                var slotNames = new[] { "morning", "afternoon", "evening" };

                for (int k = 0; k < M; k++)
                {
                    active[k] = model.NewBoolVar($"active_{k}");
                    for (int s = 0; s < 3; s++)
                    {
                        isSlot[k, s] = model.NewBoolVar($"isSlot_{k}_{s}");
                        if (!allowedBuckets[s])
                        {
                            model.Add(isSlot[k, s] == 0);
                        }
                    }
                    // Sum of isSlot over s must equal active[k]
                    model.Add(LinearExpr.Sum(new IntVar[] { isSlot[k, 0], isSlot[k, 1], isSlot[k, 2] }) == active[k]);
                }

                for (int i = 0; i < N; i++)
                {
                    var r = students[i];
                    List<string> preferred;
                    try
                    {
                        preferred = JsonSerializer.Deserialize<List<string>>(r.PreferredSlotsJson ?? "[]") ?? new List<string>();
                    }
                    catch
                    {
                        preferred = new List<string>();
                    }
                    var normalized = preferred.Select(s => s.Trim().ToLower()).ToHashSet();
                    if (!normalized.Any())
                    {
                        normalized = new HashSet<string> { "morning", "afternoon", "evening" };
                    }

                    for (int k = 0; k < M; k++)
                    {
                        x[i, k] = model.NewBoolVar($"x_{i}_{k}");

                        for (int s = 0; s < 3; s++)
                        {
                            if (!normalized.Contains(slotNames[s]))
                            {
                                model.Add(x[i, k] + isSlot[k, s] <= 1);
                            }
                        }
                    }

                    // A student is assigned to at most 1 class
                    var studentClasses = new List<IntVar>();
                    for (int k = 0; k < M; k++)
                    {
                        studentClasses.Add(x[i, k]);
                    }
                    model.Add(LinearExpr.Sum(studentClasses.ToArray()) <= 1);
                }

                // Class size constraints
                for (int k = 0; k < M; k++)
                {
                    var classStudents = new List<IntVar>();
                    for (int i = 0; i < N; i++)
                    {
                        classStudents.Add(x[i, k]);
                    }

                    model.Add(LinearExpr.Sum(classStudents.ToArray()) >= minClassSize * active[k]);
                    model.Add(LinearExpr.Sum(classStudents.ToArray()) <= maxClassSize * active[k]);
                }

                // Objective: Maximize assigned students
                var totalAssignedList = new List<IntVar>();
                for (int i = 0; i < N; i++)
                {
                    for (int k = 0; k < M; k++)
                    {
                        totalAssignedList.Add(x[i, k]);
                    }
                }
                model.Maximize(LinearExpr.Sum(totalAssignedList.ToArray()));

                var solver = new CpSolver();
                solver.StringParameters = "max_time_in_seconds:5.0";
                var status = solver.Solve(model);

                if (status == CpSolverStatus.Optimal || status == CpSolverStatus.Feasible)
                {
                    for (int k = 0; k < M; k++)
                    {
                        if (solver.Value(active[k]) == 1)
                        {
                            var studentIds = new List<int>();
                            for (int i = 0; i < N; i++)
                            {
                                if (solver.Value(x[i, k]) == 1)
                                {
                                    studentIds.Add(students[i].StudentId);
                                }
                            }

                            string chosenSlot = "evening";
                            for (int s = 0; s < 3; s++)
                            {
                                if (solver.Value(isSlot[k, s]) == 1)
                                {
                                    chosenSlot = slotNames[s];
                                    break;
                                }
                            }

                            draftClasses.Add(new DraftClass
                            {
                                CourseId = course.Id,
                                CourseCode = course.Code ?? $"C_{course.Id}",
                                CourseName = course.Name ?? "Khóa học",
                                StudentIds = studentIds,
                                PreferredSlotBucket = chosenSlot,
                                ExpectedLessons = expectedLessons,
                                EnrollType = groupEnrollType
                            });
                        }
                    }
                }
            }

            return draftClasses;
        }

        public async Task<ApiResponse<List<ClassDto>>> AutoScheduleSemesterAsync(AutoScheduleSemesterRequestDto request)
        {
            try
            {
                if (request == null)
                    return ApiResponse<List<ClassDto>>.Fail("ERR_INVALID_REQUEST", StatusCodes.Status400BadRequest);

                var semester = await _dbContext.Semesters.FindAsync(request.SemesterId);
                if (semester == null || semester.IsDeleted)
                    return ApiResponse<List<ClassDto>>.Fail("ERR_SEMESTER_NOT_FOUND", StatusCodes.Status404NotFound);

                // 1. Get all pending registrations for the semester
                var registrations = await _dbContext.StudentRegistrations
                    .Include(sr => sr.Student)
                    .Include(sr => sr.Course)
                    .Where(sr => sr.SemesterId == request.SemesterId && sr.Status == (int)StudentRegistrationStatus.Pending && !sr.Student.IsDeleted)
                    .ToListAsync();

                if (!registrations.Any())
                    return ApiResponse<List<ClassDto>>.Fail("ERR_NO_PENDING_REGISTRATIONS_FOUND", StatusCodes.Status400BadRequest);

                // Map time preference buckets → allowed slot indices
                var slotMap = new Dictionary<string, int[]>
                {
                    { "morning",   new[] { 0, 1 } },
                    { "afternoon", new[] { 2, 3 } },
                    { "evening",   new[] { 4 }    }
                };

                // Resolve global allowed slot indices based on request.Constraints.TimePreferences
                var globalAllowedSlotsList = new List<int>();
                if (request.Constraints.TimePreferences != null && request.Constraints.TimePreferences.Any())
                {
                    foreach (var pref in request.Constraints.TimePreferences)
                    {
                        var lowerPref = pref.Trim().ToLower();
                        if (slotMap.ContainsKey(lowerPref))
                        {
                            globalAllowedSlotsList.AddRange(slotMap[lowerPref]);
                        }
                    }
                }
                if (!globalAllowedSlotsList.Any())
                {
                    globalAllowedSlotsList = new List<int> { 0, 1, 2, 3, 4 };
                }
                var globalAllowedSlots = globalAllowedSlotsList.Distinct().ToArray();

                // Compute allowedBuckets for GroupStudentsIntoDraftClasses
                bool[] allowedBuckets = new bool[3]; // 0: morning, 1: afternoon, 2: evening
                allowedBuckets[0] = globalAllowedSlots.Contains(0) || globalAllowedSlots.Contains(1);
                allowedBuckets[1] = globalAllowedSlots.Contains(2) || globalAllowedSlots.Contains(3);
                allowedBuckets[2] = globalAllowedSlots.Contains(4);

                // 2. Group registrations into Draft Classes using CP-SAT solver to handle multiple slot preferences optimally
                var draftClasses = GroupStudentsIntoDraftClasses(registrations, request.MaxClassSize, request.MinClassSize, allowedBuckets);
                if (!draftClasses.Any())
                    return ApiResponse<List<ClassDto>>.Fail("ERR_NO_DRAFT_CLASSES_GENERATED", StatusCodes.Status400BadRequest);

                // 3. Load active Teachers and Rooms
                var teachers = await _dbContext.Teachers
                    .Where(t => t.Status == (int)TeacherStatus.Active && !t.IsDeleted)
                    .ToListAsync();
                var rooms = await _dbContext.Rooms
                    .Where(r => r.Status == (int)RoomStatus.Active && !r.IsDeleted)
                    .ToListAsync();
                if (!teachers.Any())
                    return ApiResponse<List<ClassDto>>.Fail("ERR_NO_ACTIVE_TEACHERS", StatusCodes.Status400BadRequest);
                if (!rooms.Any())
                    return ApiResponse<List<ClassDto>>.Fail("ERR_NO_ACTIVE_ROOMS", StatusCodes.Status400BadRequest);

                // 4. Load teacher availabilities for the semester
                var availabilities = await _dbContext.TeacherAvailabilities
                    .Where(ta => ta.SemesterId == request.SemesterId)
                    .ToListAsync();

                var teacherAvailMap = availabilities
                    .GroupBy(ta => ta.TeacherId)
                    .ToDictionary(g => g.Key, g => g.Select(ta => (ta.DayOfWeek, ta.SlotIndex)).ToHashSet());

                // 5. Fixed 5 slots
                var fixedSlots = FixedTimeSlot.All;
                int numFixed = fixedSlots.Length;

                // Allowed days (DayOfWeek)
                var allowedDays = request.Constraints.AllowWeekend
                    ? new[] { 0, 1, 2, 3, 4, 5, 6 }
                    : new[] { 1, 2, 3, 4, 5 };

                // Global allowed slots and slotMap are resolved at the top of AutoScheduleSemesterAsync

                // Filter out draft classes that do not intersect with the global allowed slots of this schedule run
                // (e.g. if run is morning-only, skip evening classes so they remain Pending)
                var filteredDraftClasses = new List<DraftClass>();
                foreach (var draft in draftClasses)
                {
                    var preferredSlots = !string.IsNullOrWhiteSpace(draft.PreferredSlotBucket) && slotMap.ContainsKey(draft.PreferredSlotBucket.ToLower())
                        ? slotMap[draft.PreferredSlotBucket.ToLower()]
                        : Array.Empty<int>();

                    var classAllowedSlots = preferredSlots.Intersect(globalAllowedSlots).ToArray();
                    if (classAllowedSlots.Any())
                    {
                        filteredDraftClasses.Add(draft);
                    }
                }

                if (!filteredDraftClasses.Any())
                {
                    return ApiResponse<List<ClassDto>>.Fail("ERR_NO_DRAFT_CLASSES_MATCH_TIME_PREFERENCES", StatusCodes.Status400BadRequest);
                }
                draftClasses = filteredDraftClasses;

                int numClasses = draftClasses.Count;
                int numTeachers = teachers.Count;
                int numRooms = rooms.Count;
                int freq = request.Constraints.SessionsPerWeek;

                // 6. Build CP-SAT model
                var model = new CpModel();

                var teacherVar = new IntVar[numClasses];
                var roomVar = new IntVar[numClasses];
                var roomPenaltyVar = new IntVar[numClasses];
                var dayVar = new IntVar[numClasses, freq];
                var slotIndexVar = new IntVar[numClasses, freq];
                var slotVar = new IntVar[numClasses, freq]; // flat = day * numFixed + slotIndex

                for (int i = 0; i < numClasses; i++)
                {
                    var draft = draftClasses[i];
                    teacherVar[i] = model.NewIntVar(0, numTeachers - 1, $"t_{i}");
                    roomVar[i] = model.NewIntVar(0, numRooms - 1, $"r_{i}");

                    // Room capacity check: room capacity must be >= class size (only for Offline classes)
                    if (draft.EnrollType != 1)
                    {
                        for (int rIdx = 0; rIdx < numRooms; rIdx++)
                        {
                            if ((rooms[rIdx].Capacity ?? int.MaxValue) < draft.Size)
                            {
                                model.Add(roomVar[i] != rIdx);
                            }
                        }
                    }

                    // Room capacity penalty: minimize (roomCapacity - classSize)
                    roomPenaltyVar[i] = model.NewIntVar(0, 9999, $"roomPenalty_{i}");
                    long[] roomPenaltiesForClass = rooms.Select(r => {
                        long cap = r.Capacity ?? 999;
                        return cap >= draft.Size ? (cap - draft.Size) : 9999;
                    }).ToArray();
                    model.AddElement(roomVar[i], roomPenaltiesForClass, roomPenaltyVar[i]);

                    // Intersect preferred slot of draft class with globally allowed slots
                    var preferredSlots = !string.IsNullOrWhiteSpace(draft.PreferredSlotBucket) && slotMap.ContainsKey(draft.PreferredSlotBucket.ToLower())
                        ? slotMap[draft.PreferredSlotBucket.ToLower()]
                        : Array.Empty<int>();

                    var classAllowedSlots = preferredSlots.Intersect(globalAllowedSlots).ToArray();
                    if (!classAllowedSlots.Any())
                    {
                        // Fallback: use all globally allowed slots if no intersection (e.g., admin restricted morning, student wanted evening)
                        classAllowedSlots = globalAllowedSlots;
                    }

                    for (int j = 0; j < freq; j++)
                    {
                        // Day variable
                        dayVar[i, j] = model.NewIntVar(allowedDays.Min(), allowedDays.Max(), $"day_{i}_{j}");
                        // Slot variable: full range, constrained below via AddBoolOr
                        slotIndexVar[i, j] = model.NewIntVar(0, numFixed - 1, $"fs_{i}_{j}");
                        slotVar[i, j] = model.NewIntVar(0, 7 * numFixed - 1, $"flat_{i}_{j}");
                        model.Add(slotVar[i, j] == dayVar[i, j] * numFixed + slotIndexVar[i, j]);

                        // Restrict dayVar to allowedDays (handles non-contiguous weekday sets)
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

                        // ALWAYS restrict slotIndexVar to exactly classAllowedSlots
                        // Using AddBoolOr ensures correctness for both contiguous and non-contiguous slot sets
                        {
                            var slotLiterals = classAllowedSlots.Select(s =>
                            {
                                var b = model.NewBoolVar($"slotOk_{i}_{j}_{s}");
                                model.Add(slotIndexVar[i, j] == s).OnlyEnforceIf(b);
                                model.Add(slotIndexVar[i, j] != s).OnlyEnforceIf(b.Not());
                                return (ILiteral)b;
                            }).ToArray();
                            model.AddBoolOr(slotLiterals);
                        }
                    }

                    // Constraint: The slot index must be the same for all sessions of a class
                    for (int j = 1; j < freq; j++)
                    {
                        model.Add(slotIndexVar[i, j] == slotIndexVar[i, 0]);
                    }

                    // Sessions of the same class: ordered days + gap constraint
                    for (int j = 0; j < freq - 1; j++)
                    {
                        if (request.Constraints.AllowConsecutiveDays)
                            model.Add(dayVar[i, j + 1] > dayVar[i, j]);
                        else
                            model.Add(dayVar[i, j + 1] >= dayVar[i, j] + 2);
                    }
                }

                // 7. Ràng buộc mới: Teacher Availability (Lịch rảnh của giáo viên)
                for (int i = 0; i < numClasses; i++)
                {
                    for (int j = 0; j < freq; j++)
                    {
                        for (int tIdx = 0; tIdx < numTeachers; tIdx++)
                        {
                            var teacher = teachers[tIdx];
                            // If teacher has NO availability set in DB → treat as available ALL slots (no restrictions)
                            if (!teacherAvailMap.ContainsKey(teacher.Id)) continue;

                            var activeSlots = teacherAvailMap[teacher.Id];
                            // If the set is empty (shouldn't happen, but guard it), skip
                            if (!activeSlots.Any()) continue;

                            foreach (int day in allowedDays)
                            {
                                for (int slot = 0; slot < numFixed; slot++)
                                {
                                    if (!activeSlots.Contains((day, slot)))
                                    {
                                        var dayMatch = model.NewBoolVar($"dm_ta_{i}_{j}_{day}_{tIdx}");
                                        var slotMatch = model.NewBoolVar($"sm_ta_{i}_{j}_{slot}_{tIdx}");

                                        model.Add(dayVar[i, j] == day).OnlyEnforceIf(dayMatch);
                                        model.Add(dayVar[i, j] != day).OnlyEnforceIf(dayMatch.Not());
                                        model.Add(slotIndexVar[i, j] == slot).OnlyEnforceIf(slotMatch);
                                        model.Add(slotIndexVar[i, j] != slot).OnlyEnforceIf(slotMatch.Not());

                                        var both = model.NewBoolVar($"both_ta_{i}_{j}_{day}_{slot}_{tIdx}");
                                        model.AddBoolAnd(new[] { dayMatch, slotMatch }).OnlyEnforceIf(both);
                                        model.AddBoolOr(new ILiteral[] { dayMatch.Not(), slotMatch.Not() }).OnlyEnforceIf(both.Not());

                                        model.Add(teacherVar[i] != tIdx).OnlyEnforceIf(both);
                                    }
                                }
                            }
                        }
                    }
                }

                // 8. No-conflict between classes being scheduled concurrently
                for (int i1 = 0; i1 < numClasses; i1++)
                for (int i2 = i1 + 1; i2 < numClasses; i2++)
                {
                    for (int j1 = 0; j1 < freq; j1++)
                    for (int j2 = 0; j2 < freq; j2++)
                    {
                        var same = model.NewBoolVar($"same_c_{i1}_{j1}_{i2}_{j2}");
                        model.Add(slotVar[i1, j1] == slotVar[i2, j2]).OnlyEnforceIf(same);
                        model.Add(slotVar[i1, j1] != slotVar[i2, j2]).OnlyEnforceIf(same.Not());
                        model.Add(teacherVar[i1] != teacherVar[i2]).OnlyEnforceIf(same);
                        model.Add(roomVar[i1] != roomVar[i2]).OnlyEnforceIf(same);
                    }
                }

                // 9. No-conflict against existing DB schedules
                var dbSchedules = await _dbContext.ClassSchedules
                    .Include(cs => cs.Class)
                    .Include(cs => cs.TimeSlot)
                    .Where(cs => cs.Class != null && !cs.Class.IsDeleted
                              && cs.ClassId.HasValue
                              && cs.ScheduleDate >= semester.StartDate && cs.ScheduleDate <= semester.EndDate)
                    .ToListAsync();

                var occupied = new Dictionary<(int day, int fi), List<(int? tId, int? rId)>>();
                foreach (var ext in dbSchedules)
                {
                    if (!ext.ScheduleDate.HasValue || ext.TimeSlot == null) continue;
                    int extDay = (int)ext.ScheduleDate.Value.DayOfWeek;
                    int fi = Array.FindIndex(fixedSlots, fs => fs.Start == ext.TimeSlot.StartTime);
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
                        int extFi = kvp.Key.fi;

                        var dayMatch = model.NewBoolVar($"dm_db_{i}_{j}_{extDay}");
                        var slotMatch = model.NewBoolVar($"sm_db_{i}_{j}_{extFi}");
                        model.Add(dayVar[i, j] == extDay).OnlyEnforceIf(dayMatch);
                        model.Add(dayVar[i, j] != extDay).OnlyEnforceIf(dayMatch.Not());
                        model.Add(slotIndexVar[i, j] == extFi).OnlyEnforceIf(slotMatch);
                        model.Add(slotIndexVar[i, j] != extFi).OnlyEnforceIf(slotMatch.Not());

                        var both = model.NewBoolVar($"both_db_{i}_{j}_{extDay}_{extFi}");
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

                // 10. Objectives: Load balancing
                IntVar? spread = null;
                if (numClasses > 1)
                {
                    var countAtSlot = new IntVar[numFixed];
                    for (int s = 0; s < numFixed; s++)
                    {
                        countAtSlot[s] = model.NewIntVar(0, numClasses, $"cnt_s_{s}");
                        var perClass = new List<IntVar>();
                        for (int i = 0; i < numClasses; i++)
                        {
                            var uses = model.NewBoolVar($"uses_s_{i}_{s}");
                            var sessionBools = new List<BoolVar>();
                            for (int j = 0; j < freq; j++)
                            {
                                var b = model.NewBoolVar($"sb_s_{i}_{j}_{s}");
                                model.Add(slotIndexVar[i, j] == s).OnlyEnforceIf(b);
                                model.Add(slotIndexVar[i, j] != s).OnlyEnforceIf(b.Not());
                                sessionBools.Add(b);
                            }
                            model.AddBoolOr(sessionBools.Cast<ILiteral>().ToArray()).OnlyEnforceIf(uses);
                            model.AddBoolAnd(sessionBools.Select(b => (ILiteral)b.Not()).ToArray()).OnlyEnforceIf(uses.Not());
                            perClass.Add(uses);
                        }
                        model.Add(countAtSlot[s] == LinearExpr.Sum(perClass.ToArray()));
                    }

                    var maxCount = model.NewIntVar(0, numClasses, "maxCnt_s");
                    var minCount = model.NewIntVar(0, numClasses, "minCnt_s");
                    model.AddMaxEquality(maxCount, countAtSlot);
                    model.AddMinEquality(minCount, countAtSlot);
                    spread = model.NewIntVar(0, numClasses, "spread_s");
                    model.Add(spread == maxCount - minCount);
                }

                IntVar? teacherSpread = null;
                if (numTeachers > 1 && numClasses > 1)
                {
                    var classesCountForTeacher = new IntVar[numTeachers];
                    for (int t = 0; t < numTeachers; t++)
                    {
                        classesCountForTeacher[t] = model.NewIntVar(0, numClasses, $"tCount_s_{t}");
                        var assignedToTeacher = new List<IntVar>();
                        for (int i = 0; i < numClasses; i++)
                        {
                            var assigned = model.NewBoolVar($"assigned_s_{i}_{t}");
                            model.Add(teacherVar[i] == t).OnlyEnforceIf(assigned);
                            model.Add(teacherVar[i] != t).OnlyEnforceIf(assigned.Not());
                            assignedToTeacher.Add(assigned);
                        }
                        model.Add(classesCountForTeacher[t] == LinearExpr.Sum(assignedToTeacher.ToArray()));
                    }

                    var maxTeacherClasses = model.NewIntVar(0, numClasses, "maxTeacherClasses_s");
                    var minTeacherClasses = model.NewIntVar(0, numClasses, "minTeacherClasses_s");
                    model.AddMaxEquality(maxTeacherClasses, classesCountForTeacher);
                    model.AddMinEquality(minTeacherClasses, classesCountForTeacher);
                    teacherSpread = model.NewIntVar(0, numClasses, "teacherSpread_s");
                    model.Add(teacherSpread == maxTeacherClasses - minTeacherClasses);
                }

                var objectiveExpressions = new List<IntVar>();
                if (spread != null) objectiveExpressions.Add(spread);
                
                if (teacherSpread != null)
                {
                    var weightedTeacherSpread = model.NewIntVar(0, numClasses * 100, "weightedTeacherSpread");
                    model.Add(weightedTeacherSpread == teacherSpread * 100);
                    objectiveExpressions.Add(weightedTeacherSpread);
                }

                // Add room capacity penalty (minimize difference between capacity and size)
                var totalRoomPenalty = model.NewIntVar(0, 999999, "totalRoomPenalty");
                model.Add(totalRoomPenalty == LinearExpr.Sum(roomPenaltyVar));
                var weightedRoomPenalty = model.NewIntVar(0, 999999 * 5, "weightedRoomPenalty");
                model.Add(weightedRoomPenalty == totalRoomPenalty * 5);
                objectiveExpressions.Add(weightedRoomPenalty);

                // ── Optimization: Minimize active teaching days & gaps for teachers ───────────
                if (numClasses > 1)
                {
                    // Pre-calculate isTeacher[i, t]
                    var isTeacher = new BoolVar[numClasses, numTeachers];
                    for (int i = 0; i < numClasses; i++)
                    {
                        for (int t = 0; t < numTeachers; t++)
                        {
                            isTeacher[i, t] = model.NewBoolVar($"is_t_opt_sem_{i}_{t}");
                            model.Add(teacherVar[i] == t).OnlyEnforceIf(isTeacher[i, t]);
                            model.Add(teacherVar[i] != t).OnlyEnforceIf(isTeacher[i, t].Not());
                        }
                    }

                    // Pre-calculate isDay[i, j, d] for d in allowedDays
                    var isDay = new Dictionary<(int classIdx, int sessionIdx, int day), BoolVar>();
                    foreach (int d in allowedDays)
                    {
                        for (int i = 0; i < numClasses; i++)
                        {
                            for (int j = 0; j < freq; j++)
                            {
                                var dayBool = model.NewBoolVar($"is_d_opt_sem_{i}_{j}_{d}");
                                model.Add(dayVar[i, j] == d).OnlyEnforceIf(dayBool);
                                model.Add(dayVar[i, j] != d).OnlyEnforceIf(dayBool.Not());
                                isDay[(i, j, d)] = dayBool;
                            }
                        }
                    }

                    // Define teacherTeachesOnDay[t, d] & gapPenalties
                    var teacherTeachesOnDay = new List<BoolVar>();
                    var gapPenalties = new List<(BoolVar Var, int Weight)>();

                    for (int t = 0; t < numTeachers; t++)
                    {
                        foreach (int d in allowedDays)
                        {
                            var tTeachesOnD = model.NewBoolVar($"t_teaches_opt_sem_{t}_{d}");
                            var activeSessions = new List<IntVar>();

                            // teachesAtSlot[s] represents if teacher t teaches on day d at slot s
                            var teachesAtSlot = new BoolVar[numFixed];
                            for (int s = 0; s < numFixed; s++)
                            {
                                teachesAtSlot[s] = model.NewBoolVar($"teaches_sem_t_{t}_d_{d}_s_{s}");
                                if (!globalAllowedSlots.Contains(s))
                                {
                                    model.Add(teachesAtSlot[s] == 0);
                                    continue;
                                }

                                var sessionsAtSlot = new List<BoolVar>();
                                for (int i = 0; i < numClasses; i++)
                                {
                                    for (int j = 0; j < freq; j++)
                                    {
                                        var isBoth = model.NewBoolVar($"is_both_opt_sem_{i}_{j}_{t}_{d}_{s}");
                                        var isSlot = model.NewBoolVar($"is_slot_opt_sem_{i}_{j}_{s}");
                                        model.Add(slotIndexVar[i, j] == s).OnlyEnforceIf(isSlot);
                                        model.Add(slotIndexVar[i, j] != s).OnlyEnforceIf(isSlot.Not());

                                        model.AddBoolAnd(new[] { isTeacher[i, t], isDay[(i, j, d)], isSlot }).OnlyEnforceIf(isBoth);
                                        model.AddBoolOr(new ILiteral[] { isTeacher[i, t].Not(), isDay[(i, j, d)].Not(), isSlot.Not() }).OnlyEnforceIf(isBoth.Not());
                                        sessionsAtSlot.Add(isBoth);
                                    }
                                }

                                model.AddBoolOr(sessionsAtSlot.Cast<ILiteral>().ToArray()).OnlyEnforceIf(teachesAtSlot[s]);
                                model.AddBoolAnd(sessionsAtSlot.Select(b => (ILiteral)b.Not()).ToArray()).OnlyEnforceIf(teachesAtSlot[s].Not());
                            }

                            for (int i = 0; i < numClasses; i++)
                            {
                                for (int j = 0; j < freq; j++)
                                {
                                    var isBoth = model.NewBoolVar($"is_both_opt_sem_{i}_{j}_{t}_{d}");
                                    model.AddBoolAnd(new[] { isTeacher[i, t], isDay[(i, j, d)] }).OnlyEnforceIf(isBoth);
                                    model.AddBoolOr(new ILiteral[] { isTeacher[i, t].Not(), isDay[(i, j, d)].Not() }).OnlyEnforceIf(isBoth.Not());
                                    activeSessions.Add(isBoth);
                                }
                            }

                            var sumExpr = LinearExpr.Sum(activeSessions.ToArray());
                            model.Add(sumExpr >= 1).OnlyEnforceIf(tTeachesOnD);
                            model.Add(sumExpr == 0).OnlyEnforceIf(tTeachesOnD.Not());
                            teacherTeachesOnDay.Add(tTeachesOnD);

                            // Calculate gaps (distance >= 3)
                            // Slot 0 and Slot 4 (distance 4) -> penalty 60
                            var gap04 = model.NewBoolVar($"gap04_sem_{t}_{d}");
                            model.AddBoolAnd(new[] { teachesAtSlot[0], teachesAtSlot[4] }).OnlyEnforceIf(gap04);
                            model.AddBoolOr(new ILiteral[] { teachesAtSlot[0].Not(), teachesAtSlot[4].Not() }).OnlyEnforceIf(gap04.Not());
                            gapPenalties.Add((gap04, 60));

                            // Slot 0 and Slot 3 (distance 3) -> penalty 30
                            var gap03 = model.NewBoolVar($"gap03_sem_{t}_{d}");
                            model.AddBoolAnd(new[] { teachesAtSlot[0], teachesAtSlot[3] }).OnlyEnforceIf(gap03);
                            model.AddBoolOr(new ILiteral[] { teachesAtSlot[0].Not(), teachesAtSlot[3].Not() }).OnlyEnforceIf(gap03.Not());
                            gapPenalties.Add((gap03, 30));

                            // Slot 1 and Slot 4 (distance 3) -> penalty 30
                            var gap14 = model.NewBoolVar($"gap14_sem_{t}_{d}");
                            model.AddBoolAnd(new[] { teachesAtSlot[1], teachesAtSlot[4] }).OnlyEnforceIf(gap14);
                            model.AddBoolOr(new ILiteral[] { teachesAtSlot[1].Not(), teachesAtSlot[4].Not() }).OnlyEnforceIf(gap14.Not());
                            gapPenalties.Add((gap14, 30));
                        }
                    }

                    var totalTeacherDays = model.NewIntVar(0, numTeachers * allowedDays.Length, "totalTeacherDays_sem");
                    model.Add(totalTeacherDays == LinearExpr.Sum(teacherTeachesOnDay.Cast<IntVar>().ToArray()));

                    var weightedTeacherDays = model.NewIntVar(0, numTeachers * allowedDays.Length * 10, "weightedTeacherDays_sem");
                    model.Add(weightedTeacherDays == totalTeacherDays * 10);
                    objectiveExpressions.Add(weightedTeacherDays);

                    // Add totalGapPenalty to objectives
                    if (gapPenalties.Any())
                    {
                        var totalGapPenalty = model.NewIntVar(0, 999999, "totalGapPenalty");
                        var gapExprs = gapPenalties.Select(gp => gp.Var * gp.Weight).ToArray();
                        model.Add(totalGapPenalty == LinearExpr.Sum(gapExprs));
                        objectiveExpressions.Add(totalGapPenalty);
                    }
                }

                if (objectiveExpressions.Any())
                {
                    if (objectiveExpressions.Count == 1)
                        model.Minimize(objectiveExpressions[0]);
                    else
                        model.Minimize(LinearExpr.Sum(objectiveExpressions.ToArray()));
                }

                // 11. Solve
                var solver = new CpSolver();
                solver.StringParameters = "max_time_in_seconds:30.0";
                var status = solver.Solve(model);

                if (status != CpSolverStatus.Feasible && status != CpSolverStatus.Optimal)
                {
                    var diagMsg = DiagnoseInfeasibility(
                        draftClasses,
                        teachers,
                        rooms,
                        teacherAvailMap,
                        allowedDays,
                        globalAllowedSlots,
                        freq,
                        numFixed);
                    return ApiResponse<List<ClassDto>>.Fail(diagMsg, StatusCodes.Status409Conflict);
                }

                // 12. Build in-memory draft result (do NOT persist to DB yet)
                var resultList = new List<ClassDto>();

                for (int i = 0; i < numClasses; i++)
                {
                    var draft = draftClasses[i];
                    int tIdx = (int)solver.Value(teacherVar[i]);
                    int rIdx = (int)solver.Value(roomVar[i]);

                    var teacher = teachers[tIdx];
                    var room = rooms[rIdx];

                    var classCode = $"{draft.CourseCode}_{semester.Code}_{draft.PreferredSlotBucket.Substring(0, 3).ToUpper()}_{i + 1}";
                    var className = $"Lớp {draft.CourseName} - {semester.Name} ({draft.PreferredSlotBucket}) - Lớp {i + 1}";

                    // Build weekly schedules for this draft class
                    var newWS = new List<WeeklyScheduleDto>();
                    for (int j = 0; j < freq; j++)
                    {
                        int dayVal = (int)solver.Value(dayVar[i, j]);
                        int fsVal = (int)solver.Value(slotIndexVar[i, j]);
                        var fs = fixedSlots[fsVal];
                        newWS.Add(new WeeklyScheduleDto
                        {
                            DayOfWeek = dayVal,
                            StartTime = fs.StartStr,
                            EndTime = fs.EndStr,
                            RoomId = draft.EnrollType == 1 ? null : room.Id
                        });
                    }

                    // Generate in-memory ClassSchedule list (no DB write)
                    var scheduleDisplay = string.Join(", ", newWS
                        .OrderBy(w => w.DayOfWeek)
                        .Select(w => $"{GetDayOfWeekName(w.DayOfWeek)} {w.StartTime}-{w.EndTime}"));

                    var inMemorySchedules = new List<ClassScheduleDto>();
                    int lessonNo = 1;
                    var orderedWS = newWS.OrderBy(w => w.DayOfWeek).ToList();

                    var cur = semester.StartDate;
                    while (cur <= semester.EndDate)
                    {
                        var match = orderedWS.FirstOrDefault(w => (int)cur.DayOfWeek == w.DayOfWeek);
                        if (match != null && TimeSpan.TryParse(match.StartTime, out var st) && TimeSpan.TryParse(match.EndTime, out _))
                        {
                            var fixedSlot = FixedTimeSlot.FromStartTime(st);
                            inMemorySchedules.Add(new ClassScheduleDto
                            {
                                LessonNo = lessonNo,
                                ScheduleDate = cur,
                                StartTime = match.StartTime,
                                EndTime = match.EndTime,
                                RoomId = draft.EnrollType == 1 ? null : room.Id,
                                RoomName = draft.EnrollType == 1 ? null : room.Name,
                                TeacherId = teacher.Id,
                                TeacherName = teacher.Name,
                                SlotName = fixedSlot?.Name,
                                Status = (int)ClassScheduleStatus.Scheduled,
                                Code = $"SCH_DRAFT_{i + 1}_{lessonNo}",
                                Name = $"Buổi học {lessonNo}"
                            });
                            lessonNo++;
                        }
                        cur = cur.AddDays(1);
                    }

                    // Build student list for draft display
                    var studentDtos = new List<ClassStudentDto>();
                    foreach (var studentId in draft.StudentIds)
                    {
                        var reg = registrations.FirstOrDefault(r => r.StudentId == studentId && r.CourseId == draft.CourseId);
                        if (reg?.Student != null)
                        {
                            studentDtos.Add(new ClassStudentDto
                            {
                                StudentId = studentId,
                                EnrollType = reg.EnrollType,
                                EnrollTypeName = reg.EnrollType == 1 ? "Online" : "Offline"
                            });
                        }
                    }

                    resultList.Add(new ClassDto
                    {
                        Id = 0, // 0 signals draft (not yet persisted)
                        Code = classCode,
                        Name = className,
                        Status = (int)ClassStatus.Planning,
                        StatusName = "Planning",
                        Type = draft.EnrollType,
                        TypeName = draft.EnrollType == 1 ? "Online" : "Offline",
                        StartDate = semester.StartDate,
                        EndDate = semester.EndDate,
                        CourseId = draft.CourseId,
                        TeacherId = teacher.Id,
                        TeacherName = teacher.Name,
                        SemesterId = semester.Id,
                        SemesterName = semester.Name,
                        ScheduleDisplay = scheduleDisplay,
                        ExpectedLessons = lessonNo - 1,
                        WeeklySchedulesJson = System.Text.Json.JsonSerializer.Serialize(newWS,
                            new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase }),
                        StudentCount = draft.StudentIds.Count,
                        Schedules = inMemorySchedules,
                        StudentClasses = studentDtos
                    });
                }

                return ApiResponse<List<ClassDto>>.Ok(resultList, "AUTO_SCHEDULING_SEMESTER_DRAFT_GENERATED");
            }
            catch (Exception ex)
            {
                return ApiResponse<List<ClassDto>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Save confirmed draft to DB
        // ─────────────────────────────────────────────────────────────────────
        public async Task<ApiResponse<List<ClassDto>>> SaveSemesterScheduleDraftAsync(SaveScheduleDraftRequestDto request)
        {
            try
            {
                if (request == null || request.Classes == null || !request.Classes.Any())
                    return ApiResponse<List<ClassDto>>.Fail("ERR_INVALID_DRAFT_REQUEST", StatusCodes.Status400BadRequest);

                var semester = await _dbContext.Semesters.FindAsync(request.SemesterId);
                if (semester == null || semester.IsDeleted)
                    return ApiResponse<List<ClassDto>>.Fail("ERR_SEMESTER_NOT_FOUND", StatusCodes.Status404NotFound);

                using var transaction = await _dbContext.Database.BeginTransactionAsync();
                try
                {
                    var createdClasses = new List<Class>();

                    foreach (var draftClass in request.Classes)
                    {
                        var entity = new Class
                        {
                            Code = draftClass.Code,
                            Name = draftClass.Name,
                            Status = (int)ClassStatus.Planning,
                            Type = draftClass.EnrollType,
                            StartDate = semester.StartDate,
                            EndDate = semester.EndDate,
                            CourseId = draftClass.CourseId,
                            TeacherId = draftClass.TeacherId,
                            SemesterId = semester.Id,
                            AutoRefund = false
                        };

                        _dbContext.Classes.Add(entity);
                        await _dbContext.SaveChangesAsync(); // get Id

                        var saveDto = new ClassSaveDto
                        {
                            Id = entity.Id,
                            Code = entity.Code ?? string.Empty,
                            Name = entity.Name ?? string.Empty,
                            Status = entity.Status,
                            StartDate = entity.StartDate,
                            EndDate = entity.EndDate,
                            SemesterId = entity.SemesterId,
                            ExpectedLessons = draftClass.ExpectedLessons,
                            TeacherId = entity.TeacherId,
                            WeeklySchedules = draftClass.WeeklySchedules
                        };

                        await GenerateClassSchedulesHelperAsync(entity, saveDto);
                        _dbContext.Classes.Update(entity);

                        // Link students and mark registrations as Scheduled
                        foreach (var studentEntry in draftClass.Students)
                        {
                            _dbContext.StudentClasses.Add(new StudentClass
                            {
                                StudentId = studentEntry.StudentId,
                                ClassId = entity.Id,
                                EnrollDate = DateTime.UtcNow,
                                Status = (int)StudentClassStatus.Enrolled,
                                EnrollType = studentEntry.EnrollType
                            });

                            var reg = await _dbContext.StudentRegistrations
                                .FirstOrDefaultAsync(r =>
                                    r.StudentId == studentEntry.StudentId &&
                                    r.CourseId == draftClass.CourseId &&
                                    r.SemesterId == request.SemesterId &&
                                    r.Status == (int)StudentRegistrationStatus.Pending);

                            if (reg != null)
                            {
                                reg.Status = (int)StudentRegistrationStatus.Scheduled;
                                _dbContext.StudentRegistrations.Update(reg);
                            }
                        }

                        createdClasses.Add(entity);
                    }

                    await _dbContext.SaveChangesAsync();
                    await transaction.CommitAsync();

                    // Reload and return persisted classes
                    var resultList = new List<ClassDto>();
                    foreach (var c in createdClasses)
                    {
                        var reloaded = await _dbContext.Classes
                            .Include(cl => cl.Course)
                            .Include(cl => cl.Teacher)
                            .Include(cl => cl.ClassSchedules).ThenInclude(cs => cs.TimeSlot)
                            .Include(cl => cl.ClassSchedules).ThenInclude(cs => cs.Room)
                            .Include(cl => cl.StudentClasses).ThenInclude(sc => sc.Student)
                            .FirstOrDefaultAsync(cl => cl.Id == c.Id);
                        if (reloaded != null) resultList.Add(MapToDto(reloaded));
                    }

                    return ApiResponse<List<ClassDto>>.Ok(resultList, "SCHEDULE_DRAFT_SAVED");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    throw new Exception("Error saving schedule draft: " + ex.Message, ex);
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

        private async Task GenerateClassSchedulesHelperAsync(Class entity, ClassSaveDto dto)
        {
            if (dto.WeeklySchedules == null || !dto.WeeklySchedules.Any() || !dto.StartDate.HasValue)
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

            // Cache: load existing DB time slots once, keyed by (StartTime, EndTime)
            var dbTimeSlotCache = await _dbContext.TimeSlots
                .Where(ts => !ts.IsDeleted)
                .ToListAsync();

            var currentDate = dto.StartDate.Value;
            var endDate = dto.EndDate;
            int lessonNo = 1;
            var weeklySchedules = dto.WeeklySchedules.OrderBy(w => w.DayOfWeek).ToList();

            if (endDate.HasValue)
            {
                entity.EndDate = endDate.Value;
                while (currentDate <= endDate.Value)
                {
                    var match = weeklySchedules.FirstOrDefault(w => (int)currentDate.DayOfWeek == w.DayOfWeek);
                    if (match != null)
                    {
                        var startSpan = TimeSpan.Parse(match.StartTime);
                        var endSpan   = TimeSpan.Parse(match.EndTime);

                        var fixedSlot = FixedTimeSlot.FromStartTime(startSpan);
                        var timeSlot = dbTimeSlotCache.FirstOrDefault(ts => ts.StartTime == startSpan && ts.EndTime == endSpan);
                        if (timeSlot == null)
                        {
                            var slotName = fixedSlot != null
                                ? fixedSlot.Name
                                : $"{match.StartTime} - {match.EndTime}";

                            timeSlot = new TimeSlot
                            {
                                Code      = $"TS_{match.StartTime.Replace(":", "")}_{match.EndTime.Replace(":", "")}",
                                Name      = slotName,
                                StartTime = startSpan,
                                EndTime   = endSpan
                            };
                            _dbContext.TimeSlots.Add(timeSlot);
                            await _dbContext.SaveChangesAsync();
                            dbTimeSlotCache.Add(timeSlot);
                        }

                        entity.ClassSchedules.Add(new ClassSchedule
                        {
                            LessonNo     = lessonNo,
                            ScheduleDate = currentDate,
                            SlotId       = timeSlot.Id,
                            RoomId       = match.RoomId,
                            TeacherId    = dto.TeacherId,
                            Status       = (int)ClassScheduleStatus.Scheduled,
                            Code         = $"SCH_{entity.Code}_{lessonNo}",
                            Name         = $"Buổi học {lessonNo} - {entity.Name}"
                        });
                        lessonNo++;
                    }
                    currentDate = currentDate.AddDays(1);
                }
                entity.ExpectedLessons = lessonNo - 1;
            }
            else
            {
                int maxLessons = dto.ExpectedLessons.GetValueOrDefault(30);
                while (lessonNo <= maxLessons)
                {
                    var match = weeklySchedules.FirstOrDefault(w => (int)currentDate.DayOfWeek == w.DayOfWeek);
                    if (match != null)
                    {
                        var startSpan = TimeSpan.Parse(match.StartTime);
                        var endSpan   = TimeSpan.Parse(match.EndTime);

                        var fixedSlot = FixedTimeSlot.FromStartTime(startSpan);
                        var timeSlot = dbTimeSlotCache.FirstOrDefault(ts => ts.StartTime == startSpan && ts.EndTime == endSpan);
                        if (timeSlot == null)
                        {
                            var slotName = fixedSlot != null
                                ? fixedSlot.Name
                                : $"{match.StartTime} - {match.EndTime}";

                            timeSlot = new TimeSlot
                            {
                                Code      = $"TS_{match.StartTime.Replace(":", "")}_{match.EndTime.Replace(":", "")}",
                                Name      = slotName,
                                StartTime = startSpan,
                                EndTime   = endSpan
                            };
                            _dbContext.TimeSlots.Add(timeSlot);
                            await _dbContext.SaveChangesAsync();
                            dbTimeSlotCache.Add(timeSlot);
                        }

                        entity.ClassSchedules.Add(new ClassSchedule
                        {
                            LessonNo     = lessonNo,
                            ScheduleDate = currentDate,
                            SlotId       = timeSlot.Id,
                            RoomId       = match.RoomId,
                            TeacherId    = dto.TeacherId,
                            Status       = (int)ClassScheduleStatus.Scheduled,
                            Code         = $"SCH_{entity.Code}_{lessonNo}",
                            Name         = $"Buổi học {lessonNo} - {entity.Name}"
                        });
                        lessonNo++;
                    }
                    currentDate = currentDate.AddDays(1);
                }
                entity.ExpectedLessons = maxLessons;
                if (entity.ClassSchedules.Any())
                {
                    entity.EndDate = entity.ClassSchedules.Last().ScheduleDate;
                }
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

        private string DiagnoseInfeasibility(
            List<DraftClass> draftClasses,
            List<Teacher> teachers,
            List<Room> rooms,
            Dictionary<int, HashSet<(int, int)>> teacherAvailMap,
            int[] allowedDays,
            int[] globalAllowedSlots,
            int freq,
            int numFixed)
        {
            var errors = new List<string>();

            // Check room capacity for each draft class
            foreach (var draft in draftClasses)
            {
                var suitableRooms = rooms.Where(r => (r.Capacity ?? int.MaxValue) >= draft.Size).ToList();
                if (!suitableRooms.Any())
                {
                    errors.Add($"Khóa học '{draft.CourseName}': Không có phòng học nào có sức chứa đủ cho sĩ số {draft.Size} (Phòng lớn nhất: {rooms.Max(r => r.Capacity ?? 0)}).");
                }
            }

            // Check teacher availability for each class
            var slotMap = new Dictionary<string, int[]>
            {
                { "morning",   new[] { 0, 1 } },
                { "afternoon", new[] { 2, 3 } },
                { "evening",   new[] { 4 }    }
            };

            foreach (var draft in draftClasses)
            {
                var preferredSlots = !string.IsNullOrWhiteSpace(draft.PreferredSlotBucket) && slotMap.ContainsKey(draft.PreferredSlotBucket.ToLower())
                    ? slotMap[draft.PreferredSlotBucket.ToLower()]
                    : Array.Empty<int>();

                var classAllowedSlots = preferredSlots.Intersect(globalAllowedSlots).ToArray();
                if (!classAllowedSlots.Any()) classAllowedSlots = globalAllowedSlots;

                bool hasTeacher = false;
                foreach (var t in teachers)
                {
                    if (!teacherAvailMap.ContainsKey(t.Id))
                    {
                        hasTeacher = true;
                        break;
                    }
                    var active = teacherAvailMap[t.Id];
                    var matchingSlots = active.Where(slot => allowedDays.Contains(slot.Item1) && classAllowedSlots.Contains(slot.Item2));
                    if (matchingSlots.Any())
                    {
                        hasTeacher = true;
                        break;
                    }
                }

                if (!hasTeacher)
                {
                    var slotNames = string.Join(", ", classAllowedSlots.Select(s => $"Ca {s + 1}"));
                    errors.Add($"Khóa học '{draft.CourseName}': Không có giáo viên nào có lịch rảnh vào các ca học được phép ({slotNames}) trong các ngày đã chọn.");
                }
            }

            // Check total room-slot capacity
            int totalSessions = draftClasses.Count * freq;
            int totalRoomSlots = allowedDays.Length * globalAllowedSlots.Length * rooms.Count;
            if (totalSessions > totalRoomSlots)
            {
                errors.Add($"Tổng số buổi học cần xếp ({totalSessions} buổi) vượt quá tổng công suất phòng học khả dụng ({totalRoomSlots} lượt). Vui lòng thêm phòng học hoặc chọn thêm ngày/ca học.");
            }

            // Check total teacher availability capacity
            int totalTeacherSlots = 0;
            foreach (var t in teachers)
            {
                if (!teacherAvailMap.ContainsKey(t.Id))
                {
                    totalTeacherSlots += allowedDays.Length * globalAllowedSlots.Length;
                }
                else
                {
                    var active = teacherAvailMap[t.Id];
                    totalTeacherSlots += active.Count(slot => allowedDays.Contains(slot.Item1) && globalAllowedSlots.Contains(slot.Item2));
                }
            }

            if (totalTeacherSlots < totalSessions)
            {
                errors.Add($"Tổng số buổi giáo viên có thể dạy ({totalTeacherSlots} buổi) ít hơn tổng số buổi học cần xếp ({totalSessions} buổi). Vui lòng thêm giáo viên hoặc mở rộng ca/ngày dạy.");
            }

            if (errors.Any())
            {
                return string.Join(" \n", errors);
            }

            return "Không tìm thấy phương án xếp lịch khả thi do xung đột ràng buộc bận giữa giáo viên, phòng học hoặc các quy định giãn cách ngày học.";
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
            SemesterId = entity.SemesterId,
            SemesterName = entity.Semester?.Name,
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
                Note = cs.Note,
                ClassStatus = cs.Class != null ? cs.Class.Status : entity.Status
            }).OrderBy(cs => cs.LessonNo).ToList() ?? new List<ClassScheduleDto>(),
            StudentClasses = entity.StudentClasses?.Select(sc => new ClassStudentDto
            {
                Id = sc.Id,
                StudentId = sc.StudentId,
                Student = sc.Student != null ? new sep490_be.DTO.Student.StudentDto
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

