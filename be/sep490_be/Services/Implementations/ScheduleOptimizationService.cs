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
using sep490_be.Repositories.Common;
using sep490_be.Repositories.Interfaces;
using sep490_be.Helpers;

namespace sep490_be.Services.Implementations
{
    public class ScheduleOptimizationService : IScheduleOptimizationService
    {
        private const int MaxScheduleVersionsPerSemester = 20;

        private readonly IClassRepository _classRepository;
        private readonly IBaseRepository<ClassSchedule, ApplicationDbContext> _scheduleRepository;
        private readonly IBaseRepository<TeacherAvailability, ApplicationDbContext> _availabilityRepository;
        private readonly ITeacherRepository _teacherRepository;
        private readonly IRoomRepository _roomRepository;
        private readonly ISemesterRepository _semesterRepository;
        private readonly IStudentRegistrationRepository _studentRegistrationRepository;
        private readonly IBaseRepository<StudentClass, ApplicationDbContext> _studentClassRepository;
        private readonly IBaseRepository<TimeSlot, ApplicationDbContext> _timeSlotRepository;
        private readonly IBaseRepository<ScheduleVersion, ApplicationDbContext> _scheduleVersionRepository;
        private readonly INotificationService _notificationService;

        public ScheduleOptimizationService(
            IClassRepository classRepository,
            IBaseRepository<ClassSchedule, ApplicationDbContext> scheduleRepository,
            IBaseRepository<TeacherAvailability, ApplicationDbContext> availabilityRepository,
            ITeacherRepository teacherRepository,
            IRoomRepository roomRepository,
            ISemesterRepository semesterRepository,
            IStudentRegistrationRepository studentRegistrationRepository,
            IBaseRepository<StudentClass, ApplicationDbContext> studentClassRepository,
            IBaseRepository<TimeSlot, ApplicationDbContext> timeSlotRepository,
            IBaseRepository<ScheduleVersion, ApplicationDbContext> scheduleVersionRepository,
            INotificationService notificationService)
        {
            _classRepository = classRepository;
            _scheduleRepository = scheduleRepository;
            _availabilityRepository = availabilityRepository;
            _teacherRepository = teacherRepository;
            _roomRepository = roomRepository;
            _semesterRepository = semesterRepository;
            _studentRegistrationRepository = studentRegistrationRepository;
            _studentClassRepository = studentClassRepository;
            _timeSlotRepository = timeSlotRepository;
            _scheduleVersionRepository = scheduleVersionRepository;
            _notificationService = notificationService;
        }

        public async Task<ApiResponse<ConflictCheckResultDto>> CheckConflictAsync(ClassSaveDto dto)
        {
            try
            {
                var result = new ConflictCheckResultDto { HasConflict = false };

                if (dto.WeeklySchedules == null || !dto.WeeklySchedules.Any() || !dto.StartDate.HasValue)
                {
                    return ApiResponse<ConflictCheckResultDto>.Ok(result, "NO_SCHEDULE_DATA_TO_CHECK");
                }

                var currentDate = dto.StartDate.Value;
                DateTime? endDate = null;
                int? expectedLessons = dto.ExpectedLessons;

                if (dto.SemesterId.HasValue && dto.SemesterId.Value > 0)
                {
                    var sem = await _semesterRepository.GetByIdAsync(dto.SemesterId.Value);
                    if (sem != null && !sem.IsDeleted)
                    {
                        currentDate = sem.StartDate;
                        endDate = sem.EndDate;
                    }
                }

                if (!endDate.HasValue)
                {
                    if (!expectedLessons.HasValue || expectedLessons.Value <= 0)
                    {
                        return ApiResponse<ConflictCheckResultDto>.Ok(result, "NO_SCHEDULE_DATA_TO_CHECK");
                    }
                }

                // Generate candidate schedules for the class in memory
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

                if (endDate.HasValue)
                {
                    while (currentDate <= endDate.Value)
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
                }
                else
                {
                    while (lessonNo <= expectedLessons.Value)
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
                }

                if (!proposedSchedules.Any())
                {
                    return ApiResponse<ConflictCheckResultDto>.Ok(result, "NO_PROPOSED_SCHEDULES_GENERATED");
                }

                var minDate = proposedSchedules.Min(p => p.Date);
                var maxDate = proposedSchedules.Max(p => p.Date);

                // Fetch existing schedules in the database for the given date range (except this class)
                var existingSchedules = await _scheduleRepository.FindAll()
                    .Include(cs => cs.Class)
                    .Include(cs => cs.TimeSlot)
                    .Include(cs => cs.Room)
                    .Include(cs => cs.Teacher)
                    .Where(cs => cs.Class != null && !cs.Class.IsDeleted && cs.ClassId != dto.Id)
                    .Where(cs => cs.ScheduleDate >= minDate && cs.ScheduleDate <= maxDate)
                    .ToListAsync();

                string teacherName = "";
                if (dto.TeacherId.HasValue)
                {
                    var teacher = await _teacherRepository.FindAll().FirstOrDefaultAsync(t => t.Id == dto.TeacherId.Value);
                    teacherName = teacher?.Name ?? "";
                }

                HashSet<(int DayOfWeek, int SlotIndex)> teacherAvails = null;
                if (dto.TeacherId.HasValue && dto.SemesterId.HasValue && dto.SemesterId.Value > 0)
                {
                    var avList = await _availabilityRepository.FindAll()
                        .Where(ta => ta.SemesterId == dto.SemesterId.Value && ta.TeacherId == dto.TeacherId.Value)
                        .ToListAsync();
                    if (avList.Any())
                    {
                        teacherAvails = avList.Select(ta => (ta.DayOfWeek, ta.SlotIndex)).ToHashSet();
                    }
                    else
                    {
                        teacherAvails = new HashSet<(int DayOfWeek, int SlotIndex)>();
                    }
                }

                var conflicts = new List<ConflictDetailDto>();

                foreach (var prop in proposedSchedules)
                {
                    // Check teacher availability
                    if (teacherAvails != null)
                    {
                        var fixedSlot = FixedTimeSlot.FromStartTime(prop.StartTime);
                        if (fixedSlot != null)
                        {
                            int dayOfWeek = (int)prop.Date.DayOfWeek;
                            if (!teacherAvails.Contains((dayOfWeek, fixedSlot.Index)))
                            {
                                conflicts.Add(new ConflictDetailDto
                                {
                                    Type = "TeacherAvailability",
                                    TeacherId = dto.TeacherId,
                                    TeacherName = teacherName,
                                    Date = prop.Date,
                                    StartTime = prop.StartTime.ToString(@"hh\:mm"),
                                    EndTime = prop.EndTime.ToString(@"hh\:mm"),
                                    SlotId = fixedSlot.Index,
                                    SlotName = fixedSlot.Name
                                });
                            }
                        }
                    }

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
                constraints.SessionsPerWeek = Math.Clamp(constraints.SessionsPerWeek, 1, 7);
                if (constraints.TimePreferences == null || !constraints.TimePreferences.Any())
                    constraints.TimePreferences = new List<string> { "morning", "afternoon", "evening" };

                // ── 1. Load all selected classes ────────────────────────────────────────
                var allClasses = await _classRepository.FindAll()
                    .Include(c => c.Course)
                    .Include(c => c.Teacher)
                    .Include(c => c.ClassSchedules)
                    .Include(c => c.StudentClasses)
                    .Where(c => classIds.Contains(c.Id) && !c.IsDeleted)
                    .ToListAsync();

                if (!allClasses.Any())
                    return ApiResponse<List<ClassDto>>.Fail("ERR_CLASSES_NOT_FOUND", StatusCodes.Status404NotFound);

                var jsonOpts = new System.Text.Json.JsonSerializerOptions
                    { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };

                var classesToSchedule = allClasses;

                // ── 2. Validate per-class requirements ──────────────────────────────────
                foreach (var c in classesToSchedule)
                {
                    if (c.CourseId == null)
                        return ApiResponse<List<ClassDto>>.Fail($"ERR_CLASS_NO_COURSE_{c.Code}", StatusCodes.Status400BadRequest);
                    if (c.StudentClasses == null || !c.StudentClasses.Any())
                        return ApiResponse<List<ClassDto>>.Fail($"ERR_CLASS_NO_STUDENTS_{c.Code}", StatusCodes.Status400BadRequest);
                }

                // ── 3. Load resources ───────────────────────────────────────────────────
                var teachersQuery = _teacherRepository.FindAll()
                    .Where(t => t.Status == (int)TeacherStatus.Active && !t.IsDeleted);
                if (constraints.TeacherIds != null && constraints.TeacherIds.Any())
                {
                    teachersQuery = teachersQuery.Where(t => constraints.TeacherIds.Contains(t.Id));
                }
                var teachers = await teachersQuery.ToListAsync();

                var roomsQuery = _roomRepository.FindAll()
                    .Where(r => r.Status == (int)RoomStatus.Active && !r.IsDeleted);
                if (constraints.RoomIds != null && constraints.RoomIds.Any())
                {
                    roomsQuery = roomsQuery.Where(r => constraints.RoomIds.Contains(r.Id));
                }
                var rooms = await roomsQuery.ToListAsync();

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
                    // Pin teacher if already assigned and still in the allowed pool
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

                var dbSchedules = await _scheduleRepository.FindAll()
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
                using var transaction = await _classRepository.BeginTransactionAsync();
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
                            await _scheduleRepository.DeleteRangeAsync(entity.ClassSchedules);

                        await GenerateClassSchedulesHelperAsync(entity, saveDto);
                        await _classRepository.UpdateAsync(entity);
                    }

                    await _classRepository.SaveChangesAsync();
                    await transaction.CommitAsync();

                    // Return all classes in the original selection (scheduled + already-had-schedule)
                    var resultList = new List<ClassDto>();
                    foreach (var c in allClasses)
                    {
                        var reloaded = await _classRepository.FindAll()
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
            public int PreferredSlotIndex { get; set; } // 0-4
            public int PreferredDaysOfWeek { get; set; } // Bitmask
            public int ExpectedLessons { get; set; } = 30;
            public int EnrollType { get; set; } = 0; // 0 = Offline, 1 = Online
            public GradeLevel? RequiredGradeLevel { get; set; }
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

                // Variables:
                // x[i, k] = 1 if student i is assigned to class k
                var x = new BoolVar[N, M];
                var active = new BoolVar[M];
                
                // For each class k, we need to decide its preferred slot index (0-4) and its days of week bitmask (0-127)
                // To keep it simple and optimized for CP-SAT:
                // We define isSlot[k, s] = 1 if class k is at slot s (s from 0 to 4)
                var isSlot = new BoolVar[M, 5];
                
                // We define hasDay[k, d] = 1 if class k uses dayOfWeek d (d from 0 to 6)
                var hasDay = new BoolVar[M, 7];

                for (int k = 0; k < M; k++)
                {
                    active[k] = model.NewBoolVar($"active_{k}");
                    
                    // Sum of isSlot over s must equal active[k]
                    var slotVars = new List<IntVar>();
                    for (int s = 0; s < 5; s++)
                    {
                        isSlot[k, s] = model.NewBoolVar($"isSlot_{k}_{s}");
                        slotVars.Add(isSlot[k, s]);
                    }
                    model.Add(LinearExpr.Sum(slotVars.ToArray()) == active[k]);

                    for (int d = 0; d < 7; d++)
                    {
                        hasDay[k, d] = model.NewBoolVar($"hasDay_{k}_{d}");
                        // If class is inactive, it cannot use any day
                        model.Add(hasDay[k, d] <= active[k]);
                    }
                }

                // Resolve student preferences and add compatibility constraints
                for (int i = 0; i < N; i++)
                {
                    var r = students[i];
                    int prefSlot = 4; // Default Evening (slot 4)
                    int prefDaysMask = 127; // Default all days

                    if (r.PreferredSlotIndex.HasValue && r.PreferredDaysOfWeek.HasValue)
                    {
                        prefSlot = r.PreferredSlotIndex.Value;
                        prefDaysMask = r.PreferredDaysOfWeek.Value;
                    }
                    else
                    {
                        // Backward compatibility logic: Parse PreferredSlotsJson
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
                        if (normalized.Contains("morning"))
                        {
                            prefSlot = 0; // map to ca 1
                            prefDaysMask = 62; // Mon-Fri
                        }
                        else if (normalized.Contains("afternoon"))
                        {
                            prefSlot = 2; // map to ca 3
                            prefDaysMask = 62; // Mon-Fri
                        }
                        else
                        {
                            prefSlot = 4; // evening
                            prefDaysMask = 62; // Mon-Fri
                        }
                    }

                    // Get list of allowed days
                    var allowedDays = new List<int>();
                    for (int d = 0; d < 7; d++)
                    {
                        if ((prefDaysMask & (1 << d)) != 0)
                        {
                            allowedDays.Add(d);
                        }
                    }
                    if (!allowedDays.Any()) allowedDays = new List<int> { 1, 2, 3, 4, 5 }; // Fallback Mon-Fri

                    for (int k = 0; k < M; k++)
                    {
                        x[i, k] = model.NewBoolVar($"x_{i}_{k}");

                        // If student i is assigned to class k, the class must choose the student's preferred slot
                        // i.e., x[i, k] <= isSlot[k, prefSlot]
                        model.Add(x[i, k] <= isSlot[k, prefSlot]);

                        // If student i is assigned to class k, the class days must be a subset of the student's preferred days.
                        // For any day d that the class uses, if it's NOT in student's preferred days, student cannot be in class k.
                        for (int d = 0; d < 7; d++)
                        {
                            if (!allowedDays.Contains(d))
                            {
                                // x[i, k] + hasDay[k, d] <= 1
                                model.Add(x[i, k] + hasDay[k, d] <= 1);
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

                            int chosenSlot = 4;
                            for (int s = 0; s < 5; s++)
                            {
                                if (solver.Value(isSlot[k, s]) == 1)
                                {
                                    chosenSlot = s;
                                    break;
                                }
                            }

                            int chosenDaysMask = 0;
                            for (int d = 0; d < 7; d++)
                            {
                                if (solver.Value(hasDay[k, d]) == 1)
                                {
                                    chosenDaysMask |= (1 << d);
                                }
                            }
                            // If no days were active for some reason, fallback to Monday (2) and Wednesday (8) = 10
                            if (chosenDaysMask == 0) chosenDaysMask = 10;

                            draftClasses.Add(new DraftClass
                            {
                                CourseId = course.Id,
                                CourseCode = course.Code ?? $"C_{course.Id}",
                                CourseName = course.Name ?? "Khóa học",
                                StudentIds = studentIds,
                                PreferredSlotIndex = chosenSlot,
                                PreferredDaysOfWeek = chosenDaysMask,
                                ExpectedLessons = expectedLessons,
                                EnrollType = groupEnrollType,
                                RequiredGradeLevel = course.RequiredGradeLevel
                            });
                        }
                    }
                }
            }

            return draftClasses;
        }

        public async Task<ApiResponse<AutoScheduleSemesterResultDto>> AutoScheduleSemesterAsync(AutoScheduleSemesterRequestDto request)
        {
            try
            {
                if (request == null)
                    return ApiResponse<AutoScheduleSemesterResultDto>.Fail("ERR_INVALID_REQUEST", StatusCodes.Status400BadRequest);

                var semester = await _semesterRepository.GetByIdAsync(request.SemesterId);
                if (semester == null || semester.IsDeleted)
                    return ApiResponse<AutoScheduleSemesterResultDto>.Fail("ERR_SEMESTER_NOT_FOUND", StatusCodes.Status404NotFound);

                // 1. Get all pending registrations for the semester
                var registrations = await _studentRegistrationRepository.FindAll()
                    .Include(sr => sr.Student)
                    .Include(sr => sr.Course)
                    .Where(sr => sr.SemesterId == request.SemesterId && sr.Status == (int)StudentRegistrationStatus.Pending && !sr.Student.IsDeleted)
                    .ToListAsync();

                if (!registrations.Any())
                    return ApiResponse<AutoScheduleSemesterResultDto>.Fail("ERR_NO_PENDING_REGISTRATIONS_FOUND", StatusCodes.Status400BadRequest);

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

                // 2. Group registrations into Draft Classes using CP-SAT solver to handle multiple slot preferences optimally
                var draftClasses = GroupStudentsIntoDraftClasses(registrations, request.MaxClassSize, request.MinClassSize, null);
                if (!draftClasses.Any())
                    return ApiResponse<AutoScheduleSemesterResultDto>.Fail("ERR_NO_DRAFT_CLASSES_GENERATED", StatusCodes.Status400BadRequest);

                // 3. Load active Teachers and Rooms
                var teachersQuery = _teacherRepository.FindAll()
                    .Where(t => t.Status == (int)TeacherStatus.Active && !t.IsDeleted);
                if (request.Constraints.TeacherIds != null && request.Constraints.TeacherIds.Any())
                {
                    teachersQuery = teachersQuery.Where(t => request.Constraints.TeacherIds.Contains(t.Id));
                }
                var teachers = await teachersQuery.ToListAsync();

                var roomsQuery = _roomRepository.FindAll()
                    .Where(r => r.Status == (int)RoomStatus.Active && !r.IsDeleted);
                if (request.Constraints.RoomIds != null && request.Constraints.RoomIds.Any())
                {
                    roomsQuery = roomsQuery.Where(r => request.Constraints.RoomIds.Contains(r.Id));
                }
                var rooms = await roomsQuery.ToListAsync();

                if (!teachers.Any())
                    return ApiResponse<AutoScheduleSemesterResultDto>.Fail("ERR_NO_ACTIVE_TEACHERS", StatusCodes.Status400BadRequest);
                if (!rooms.Any())
                    return ApiResponse<AutoScheduleSemesterResultDto>.Fail("ERR_NO_ACTIVE_ROOMS", StatusCodes.Status400BadRequest);

                // 4. Load teacher availabilities for the semester
                var availabilities = await _availabilityRepository.FindAll()
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

                // We keep all generated draft classes since they now target specific slot indices directly
                int numClasses = draftClasses.Count;
                int numTeachers = teachers.Count;
                int numRooms = rooms.Count;
                int freq = request.Constraints.SessionsPerWeek;

                // 6. Build CP-SAT model
                var model = new CpModel();

                var teacherVar = new IntVar[numClasses];
                var roomVar = new IntVar[numClasses];
                var roomPenaltyVar = new IntVar[numClasses];
                var gradeWasteVar = new IntVar[numClasses];
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

                    // ── Teacher Grade Level Qualification & Waste Penalty ────────────
                    // Hard Constraint: Teacher's band must be >= Course's required band
                    if (draft.RequiredGradeLevel.HasValue)
                    {
                        int reqLevel = (int)draft.RequiredGradeLevel.Value;
                        for (int tIdx = 0; tIdx < numTeachers; tIdx++)
                        {
                            var t = teachers[tIdx];
                            // If teacher has no grade level specified, or their level < course requirement -> CANNOT teach this class
                            int tLevel = t.GradeLevel.HasValue ? (int)t.GradeLevel.Value : 0;
                            if (tLevel < reqLevel)
                            {
                                model.Add(teacherVar[i] != tIdx);
                            }
                        }
                    }

                    // Soft Penalty: Minimize grade level waste (teacher level - required level)
                    // This ensures high-band teachers are prioritized for high-band courses first,
                    // and lower-band teachers (who qualify) take the remaining classes.
                    gradeWasteVar[i] = model.NewIntVar(0, 9999, $"gradeWaste_{i}");
                    long[] gradeWasteForClass = teachers.Select(t => {
                        int tLevel = t.GradeLevel.HasValue ? (int)t.GradeLevel.Value : 65;
                        int reqLevel = draft.RequiredGradeLevel.HasValue ? (int)draft.RequiredGradeLevel.Value : 65;
                        return tLevel >= reqLevel ? (long)(tLevel - reqLevel) : 9999L;
                    }).ToArray();
                    model.AddElement(teacherVar[i], gradeWasteForClass, gradeWasteVar[i]);

                    // Allowed Days for this draft class based on bitmask
                    var classAllowedDaysList = new List<int>();
                    for (int d = 0; d < 7; d++)
                    {
                        if ((draft.PreferredDaysOfWeek & (1 << d)) != 0)
                        {
                            classAllowedDaysList.Add(d);
                        }
                    }
                    // Filter allowed days to Mon-Fri if weekend is not allowed
                    var classAllowedDays = classAllowedDaysList.Intersect(allowedDays).ToArray();
                    if (!classAllowedDays.Any())
                    {
                        classAllowedDays = allowedDays; // fallback to all general allowed days
                    }

                    for (int j = 0; j < freq; j++)
                    {
                        // Day variable: restricted to classAllowedDays
                        dayVar[i, j] = model.NewIntVar(classAllowedDays.Min(), classAllowedDays.Max(), $"day_{i}_{j}");
                        // Slot variable: pin to exact PreferredSlotIndex
                        slotIndexVar[i, j] = model.NewIntVar(draft.PreferredSlotIndex, draft.PreferredSlotIndex, $"fs_{i}_{j}");
                        slotVar[i, j] = model.NewIntVar(0, 7 * numFixed - 1, $"flat_{i}_{j}");
                        model.Add(slotVar[i, j] == dayVar[i, j] * numFixed + slotIndexVar[i, j]);

                        // Restrict dayVar to classAllowedDays
                        if (classAllowedDays.Length < classAllowedDays.Max() - classAllowedDays.Min() + 1)
                        {
                            var dayLiterals = classAllowedDays.Select(d =>
                            {
                                var b = model.NewBoolVar($"dayOk_{i}_{j}_{d}");
                                model.Add(dayVar[i, j] == d).OnlyEnforceIf(b);
                                model.Add(dayVar[i, j] != d).OnlyEnforceIf(b.Not());
                                return (ILiteral)b;
                            }).ToArray();
                            model.AddBoolOr(dayLiterals);
                        }
                    }

                    // Constraint: The slot index must be the same for all sessions of a class
                    for (int j = 1; j < freq; j++)
                    {
                        model.Add(slotIndexVar[i, j] == slotIndexVar[i, 0]);
                    }

                    // Sessions of the same class: must be in different days (no gap constraint, just different)
                    for (int j1 = 0; j1 < freq; j1++)
                    {
                        for (int j2 = j1 + 1; j2 < freq; j2++)
                        {
                            model.Add(dayVar[i, j1] != dayVar[i, j2]);
                        }
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
                var dbSchedules = await _scheduleRepository.FindAll()
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

                // Add grade waste penalty (minimize assigning high-band teachers to lower-band courses)
                var totalGradeWaste = model.NewIntVar(0, 999999, "totalGradeWaste");
                model.Add(totalGradeWaste == LinearExpr.Sum(gradeWasteVar));
                var weightedGradeWaste = model.NewIntVar(0, 999999 * 20, "weightedGradeWaste");
                model.Add(weightedGradeWaste == totalGradeWaste * 20);
                objectiveExpressions.Add(weightedGradeWaste);

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
                    var reasons = DiagnoseInfeasibility(
                        draftClasses,
                        teachers,
                        rooms,
                        teacherAvailMap,
                        allowedDays,
                        globalAllowedSlots,
                        freq,
                        numFixed);
                    return new ApiResponse<AutoScheduleSemesterResultDto>
                    {
                        Success = false,
                        StatusCode = StatusCodes.Status409Conflict,
                        Message = "ERR_SCHEDULE_INFEASIBLE",
                        Data = new AutoScheduleSemesterResultDto { InfeasibilityReasons = reasons }
                    };
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

                    var coursePrefix = new string(draft.CourseCode.TakeWhile(c => !char.IsDigit(c)).ToArray());
                    if (string.IsNullOrEmpty(coursePrefix))
                    {
                        coursePrefix = "KH";
                    }
                    var classCode = $"{coursePrefix}{DateTime.Now:ddMMHH}_{semester.Code}_CA{draft.PreferredSlotIndex + 1}_{i + 1}";
                    var className = $"Lớp {draft.CourseName} - {semester.Name} (Ca {draft.PreferredSlotIndex + 1}) - Lớp {i + 1}";

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

                // 13. Build reliability report: solver confidence, coverage, independent hard-constraint
                // validation and human-readable soft-constraint/descriptive metrics for the UI.
                var reliability = BuildSemesterReliabilityReport(
                    resultList, draftClasses, registrations, teachers, rooms,
                    teacherAvailMap, dbSchedules, status, freq);

                return ApiResponse<AutoScheduleSemesterResultDto>.Ok(
                    new AutoScheduleSemesterResultDto { Classes = resultList, Reliability = reliability },
                    "AUTO_SCHEDULING_SEMESTER_DRAFT_GENERATED");
            }
            catch (Exception ex)
            {
                return ApiResponse<AutoScheduleSemesterResultDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Reliability report for AutoScheduleSemesterAsync — lets the UI show, without manual
        // checking, whether a generated schedule is trustworthy: solver confidence, how many
        // requested students actually got placed, an independent re-check of every hard
        // constraint against the produced output, and soft-constraint/descriptive metrics
        // expressed in real business units with named drill-down (which teacher/room/class).
        // ─────────────────────────────────────────────────────────────────────
        private ScheduleReliabilityReportDto BuildSemesterReliabilityReport(
            List<ClassDto> resultList,
            List<DraftClass> draftClasses,
            List<StudentRegistration> registrations,
            List<Teacher> teachers,
            List<Room> rooms,
            Dictionary<int, HashSet<(int, int)>> teacherAvailMap,
            List<ClassSchedule> dbSchedules,
            CpSolverStatus status,
            int sessionsPerWeek)
        {
            var report = new ScheduleReliabilityReportDto
            {
                SolverStatus = status.ToString(),
                IsProvenOptimal = status == CpSolverStatus.Optimal
            };

            // Coverage: how many of the requested students actually got placed into a class.
            var scheduledStudentIds = draftClasses.SelectMany(d => d.StudentIds).ToHashSet();
            var draftGroupKeys = draftClasses.Select(d => (d.CourseId, d.EnrollType)).ToHashSet();
            report.Coverage.TotalRequested = registrations.Count;
            report.Coverage.TotalScheduled = registrations.Count(r => scheduledStudentIds.Contains(r.StudentId));
            report.Coverage.ScheduledRatePercent = report.Coverage.TotalRequested == 0
                ? 100
                : Math.Round(100.0 * report.Coverage.TotalScheduled / report.Coverage.TotalRequested, 1);

            foreach (var r in registrations.Where(r => !scheduledStudentIds.Contains(r.StudentId)))
            {
                bool hasGroupClass = draftGroupKeys.Contains((r.CourseId, r.EnrollType));
                report.Coverage.UnscheduledStudents.Add(new UnscheduledStudentDto
                {
                    StudentId = r.StudentId,
                    StudentName = r.Student?.Name ?? $"HS #{r.StudentId}",
                    CourseId = r.CourseId,
                    CourseName = r.Course?.Name ?? $"Khóa học #{r.CourseId}",
                    ReasonCode = hasGroupClass ? "TIME_MISMATCH" : "NO_GROUP_CLASS"
                });
            }

            report.HardConstraintChecks = ValidateHardConstraints(resultList, draftClasses, teachers, rooms, teacherAvailMap, dbSchedules);
            report.SoftMetrics = BuildSoftMetrics(resultList, draftClasses, teachers, rooms, sessionsPerWeek);
            report.DescriptiveStats = BuildDescriptiveStats(resultList, draftClasses, teachers, rooms);

            return report;
        }

        // Independent re-check of every hard constraint against the produced output — decoupled from
        // the CP-SAT model itself, so a bug in the constraint formulation doesn't silently pass as valid.
        private List<HardConstraintCheckDto> ValidateHardConstraints(
            List<ClassDto> resultList,
            List<DraftClass> draftClasses,
            List<Teacher> teachers,
            List<Room> rooms,
            Dictionary<int, HashSet<(int, int)>> teacherAvailMap,
            List<ClassSchedule> dbSchedules)
        {
            var checks = new List<HardConstraintCheckDto>();

            var sessions = resultList
                .SelectMany(cls => cls.Schedules.Select(s => new { Cls = cls, Session = s }))
                .Where(x => x.Session.ScheduleDate.HasValue)
                .ToList();

            // Teacher double-booking across the generated classes
            var teacherConflicts = new List<string>();
            foreach (var grp in sessions.Where(x => x.Session.TeacherId.HasValue)
                         .GroupBy(x => (x.Session.ScheduleDate!.Value.Date, x.Session.StartTime, x.Session.TeacherId)))
            {
                var classNames = grp.Select(x => x.Cls.Name).Distinct().ToList();
                if (classNames.Count > 1)
                {
                    teacherConflicts.Add($"Giáo viên {grp.First().Session.TeacherName} bị trùng lịch ngày {grp.Key.Date:dd/MM} ({grp.First().Session.StartTime}-{grp.First().Session.EndTime}) giữa các lớp: {string.Join(", ", classNames)}.");
                }
            }
            checks.Add(new HardConstraintCheckDto { Name = "Trùng lịch giáo viên", ViolationCount = teacherConflicts.Count, ViolationDetails = teacherConflicts });

            // Room double-booking across the generated classes
            var roomConflicts = new List<string>();
            foreach (var grp in sessions.Where(x => x.Session.RoomId.HasValue)
                         .GroupBy(x => (x.Session.ScheduleDate!.Value.Date, x.Session.StartTime, x.Session.RoomId)))
            {
                var classNames = grp.Select(x => x.Cls.Name).Distinct().ToList();
                if (classNames.Count > 1)
                {
                    roomConflicts.Add($"Phòng {grp.First().Session.RoomName} bị trùng lịch ngày {grp.Key.Date:dd/MM} ({grp.First().Session.StartTime}-{grp.First().Session.EndTime}) giữa các lớp: {string.Join(", ", classNames)}.");
                }
            }
            checks.Add(new HardConstraintCheckDto { Name = "Trùng lịch phòng học", ViolationCount = roomConflicts.Count, ViolationDetails = roomConflicts });

            // Room capacity vs class size (Offline classes only)
            var capacityViolations = new List<string>();
            for (int i = 0; i < resultList.Count; i++)
            {
                var draft = draftClasses[i];
                if (draft.EnrollType == 1) continue;
                var roomId = resultList[i].Schedules.FirstOrDefault()?.RoomId;
                var room = rooms.FirstOrDefault(r => r.Id == roomId);
                if (room != null && (room.Capacity ?? int.MaxValue) < resultList[i].StudentCount)
                {
                    capacityViolations.Add($"Lớp {resultList[i].Name} ({resultList[i].StudentCount} HS) vượt sức chứa phòng {room.Name} ({room.Capacity} chỗ).");
                }
            }
            checks.Add(new HardConstraintCheckDto { Name = "Vượt sức chứa phòng học", ViolationCount = capacityViolations.Count, ViolationDetails = capacityViolations });

            // Sessions placed outside a teacher's declared availability
            var availabilityViolations = new List<string>();
            foreach (var cls in resultList)
            {
                if (!cls.TeacherId.HasValue || !teacherAvailMap.TryGetValue(cls.TeacherId.Value, out var active)) continue;
                foreach (var s in cls.Schedules)
                {
                    if (!s.ScheduleDate.HasValue || !TimeSpan.TryParse(s.StartTime, out var st)) continue;
                    var fs = FixedTimeSlot.FromStartTime(st);
                    if (fs == null) continue;
                    int day = (int)s.ScheduleDate.Value.DayOfWeek;
                    if (!active.Contains((day, fs.Index)))
                    {
                        availabilityViolations.Add($"Giáo viên {cls.TeacherName} được xếp dạy lớp {cls.Name} ngày {s.ScheduleDate:dd/MM} ({fs.Name}) ngoài giờ đã khai báo rảnh.");
                    }
                }
            }
            checks.Add(new HardConstraintCheckDto { Name = "Ngoài giờ rảnh đã khai báo của giáo viên", ViolationCount = availabilityViolations.Count, ViolationDetails = availabilityViolations });

            // Teacher grade-band below the course's required band
            var gradeViolations = new List<string>();
            for (int i = 0; i < resultList.Count; i++)
            {
                var draft = draftClasses[i];
                if (!draft.RequiredGradeLevel.HasValue) continue;
                var teacher = teachers.FirstOrDefault(t => t.Id == resultList[i].TeacherId);
                if (teacher == null) continue;
                int tLevel = teacher.GradeLevel.HasValue ? (int)teacher.GradeLevel.Value : 0;
                if (tLevel < (int)draft.RequiredGradeLevel.Value)
                {
                    gradeViolations.Add($"Lớp {resultList[i].Name} yêu cầu band {draft.RequiredGradeLevel.Value.GetStringValue()}, giáo viên {teacher.Name} chỉ đạt {(teacher.GradeLevel.HasValue ? teacher.GradeLevel.Value.GetStringValue() : "chưa xác định")}.");
                }
            }
            checks.Add(new HardConstraintCheckDto { Name = "Giáo viên dưới band yêu cầu khóa học", ViolationCount = gradeViolations.Count, ViolationDetails = gradeViolations });

            // Conflict against schedules already committed to the database
            var dbConflicts = new List<string>();
            foreach (var cls in resultList)
            {
                foreach (var s in cls.Schedules)
                {
                    if (!s.ScheduleDate.HasValue || !TimeSpan.TryParse(s.StartTime, out var st)) continue;
                    foreach (var ext in dbSchedules)
                    {
                        if (!ext.ScheduleDate.HasValue || ext.TimeSlot == null) continue;
                        if (ext.ScheduleDate.Value.Date != s.ScheduleDate.Value.Date || ext.TimeSlot.StartTime != st) continue;

                        if (s.TeacherId.HasValue && ext.TeacherId.HasValue && s.TeacherId == ext.TeacherId)
                            dbConflicts.Add($"Lớp {cls.Name} ngày {s.ScheduleDate:dd/MM} ({s.StartTime}-{s.EndTime}) trùng giáo viên với lịch đã có trong hệ thống.");
                        if (s.RoomId.HasValue && ext.RoomId.HasValue && s.RoomId == ext.RoomId)
                            dbConflicts.Add($"Lớp {cls.Name} ngày {s.ScheduleDate:dd/MM} ({s.StartTime}-{s.EndTime}) trùng phòng với lịch đã có trong hệ thống.");
                    }
                }
            }
            checks.Add(new HardConstraintCheckDto { Name = "Trùng lịch với dữ liệu đã có trong hệ thống", ViolationCount = dbConflicts.Count, ViolationDetails = dbConflicts });

            return checks;
        }

        // Soft-constraint metrics in real business units (buổi/tuần, %, chỗ ngồi), each with a
        // threshold-derived status and named drill-down — mirrors exactly what the CP-SAT objective
        // function optimized for, so a bad number here explains why the objective value is what it is.
        private List<SoftMetricDto> BuildSoftMetrics(
            List<ClassDto> resultList,
            List<DraftClass> draftClasses,
            List<Teacher> teachers,
            List<Room> rooms,
            int sessionsPerWeek)
        {
            var metrics = new List<SoftMetricDto>();
            if (!resultList.Any()) return metrics;

            // Teacher load balance
            var teacherLoad = resultList
                .Where(c => c.TeacherId.HasValue)
                .GroupBy(c => c.TeacherId!.Value)
                .Select(g => new
                {
                    TeacherName = teachers.FirstOrDefault(t => t.Id == g.Key)?.Name ?? $"GV #{g.Key}",
                    ClassCount = g.Count(),
                    SessionsPerWeek = g.Count() * sessionsPerWeek
                })
                .OrderByDescending(x => x.SessionsPerWeek)
                .ToList();
            if (teacherLoad.Count > 1)
            {
                var max = teacherLoad.First();
                var min = teacherLoad.Last();
                int diff = max.SessionsPerWeek - min.SessionsPerWeek;
                metrics.Add(new SoftMetricDto
                {
                    Name = "Cân bằng tải giáo viên",
                    DisplayValue = $"Chênh lệch {diff} buổi/tuần",
                    Status = diff <= 2 ? "Good" : diff <= 5 ? "Warning" : "Bad",
                    Details = new List<string>
                    {
                        $"Cao nhất: {max.TeacherName} — {max.SessionsPerWeek} buổi/tuần ({max.ClassCount} lớp)",
                        $"Thấp nhất: {min.TeacherName} — {min.SessionsPerWeek} buổi/tuần ({min.ClassCount} lớp)"
                    }
                });
            }

            // Room load balance (Offline classes only — Online classes have no room)
            var roomLoad = resultList
                .Select(c => c.Schedules.FirstOrDefault()?.RoomId)
                .Where(id => id.HasValue)
                .GroupBy(id => id!.Value)
                .Select(g => new
                {
                    RoomName = rooms.FirstOrDefault(r => r.Id == g.Key)?.Name ?? $"Phòng #{g.Key}",
                    ClassCount = g.Count(),
                    SessionsPerWeek = g.Count() * sessionsPerWeek
                })
                .OrderByDescending(x => x.SessionsPerWeek)
                .ToList();
            if (roomLoad.Count > 1)
            {
                var max = roomLoad.First();
                var min = roomLoad.Last();
                int diff = max.SessionsPerWeek - min.SessionsPerWeek;
                metrics.Add(new SoftMetricDto
                {
                    Name = "Cân bằng tải phòng học",
                    DisplayValue = $"Chênh lệch {diff} buổi/tuần",
                    Status = diff <= 2 ? "Good" : diff <= 6 ? "Warning" : "Bad",
                    Details = new List<string>
                    {
                        $"Cao nhất: Phòng {max.RoomName} — {max.SessionsPerWeek} buổi/tuần ({max.ClassCount} lớp)",
                        $"Thấp nhất: Phòng {min.RoomName} — {min.SessionsPerWeek} buổi/tuần ({min.ClassCount} lớp)"
                    }
                });
            }

            // Room fill rate (Offline classes only)
            var fillRates = new List<(string ClassName, string RoomName, int Size, int Capacity)>();
            for (int i = 0; i < resultList.Count; i++)
            {
                if (draftClasses[i].EnrollType == 1) continue;
                var roomId = resultList[i].Schedules.FirstOrDefault()?.RoomId;
                var room = rooms.FirstOrDefault(r => r.Id == roomId);
                if (room?.Capacity is int cap && cap > 0)
                    fillRates.Add((resultList[i].Name, room.Name, resultList[i].StudentCount, cap));
            }
            if (fillRates.Any())
            {
                double avgRate = fillRates.Average(x => 100.0 * x.Size / x.Capacity);
                var lowest = fillRates.OrderBy(x => (double)x.Size / x.Capacity).First();
                metrics.Add(new SoftMetricDto
                {
                    Name = "Hiệu suất sử dụng phòng học",
                    DisplayValue = $"Lấp đầy trung bình {avgRate:F0}%",
                    Status = avgRate >= 80 ? "Good" : avgRate >= 60 ? "Warning" : "Bad",
                    Details = new List<string>
                    {
                        $"Thấp nhất: Lớp {lowest.ClassName} — {lowest.Size}/{lowest.Capacity} chỗ ({100.0 * lowest.Size / lowest.Capacity:F0}%) tại phòng {lowest.RoomName}"
                    }
                });
            }

            // Teacher grade-band waste (how far above the course's required band the assigned teacher is)
            var wasteList = new List<(string ClassName, string TeacherName, int WasteBands)>();
            for (int i = 0; i < resultList.Count; i++)
            {
                var draft = draftClasses[i];
                if (!draft.RequiredGradeLevel.HasValue) continue;
                var teacher = teachers.FirstOrDefault(t => t.Id == resultList[i].TeacherId);
                if (teacher?.GradeLevel == null) continue;
                int wasteBands = ((int)teacher.GradeLevel.Value - (int)draft.RequiredGradeLevel.Value) / 5;
                wasteList.Add((resultList[i].Name, teacher.Name, wasteBands));
            }
            if (wasteList.Any())
            {
                int matched = wasteList.Count(x => x.WasteBands == 0);
                double pct = 100.0 * matched / wasteList.Count;
                metrics.Add(new SoftMetricDto
                {
                    Name = "Phù hợp năng lực giáo viên theo band khóa học",
                    DisplayValue = $"{matched}/{wasteList.Count} lớp có giáo viên đúng band yêu cầu ({pct:F0}%)",
                    Status = pct >= 90 ? "Good" : pct >= 75 ? "Warning" : "Bad",
                    Details = wasteList.Where(x => x.WasteBands > 0)
                        .Select(x => $"Lớp {x.ClassName}: giáo viên {x.TeacherName} cao hơn {x.WasteBands} bậc band so với yêu cầu")
                        .ToList()
                });
            }

            // Gaps in a teacher's teaching day (idle slots between two sessions on the same day)
            var byTeacherDate = resultList
                .Where(c => c.TeacherId.HasValue)
                .SelectMany(c => c.Schedules.Select(s => new { c.TeacherId, c.TeacherName, s.ScheduleDate, s.StartTime }))
                .Where(x => x.ScheduleDate.HasValue && x.StartTime != null)
                .GroupBy(x => (x.TeacherId!.Value, x.ScheduleDate!.Value.Date))
                .ToList();
            if (byTeacherDate.Any())
            {
                var gapDetails = new List<string>();
                var flaggedTeachers = new HashSet<int>();
                foreach (var grp in byTeacherDate)
                {
                    var slotIndices = grp
                        .Select(x => TimeSpan.TryParse(x.StartTime, out var st) ? FixedTimeSlot.FromStartTime(st)?.Index : null)
                        .Where(idx => idx.HasValue)
                        .Select(idx => idx!.Value)
                        .Distinct()
                        .OrderBy(x => x)
                        .ToList();
                    if (slotIndices.Count < 2) continue;
                    int span = slotIndices.Last() - slotIndices.First() + 1;
                    if (span > slotIndices.Count)
                    {
                        var teacherName = grp.First().TeacherName ?? $"GV #{grp.Key.Item1}";
                        gapDetails.Add($"{teacherName}: có giờ trống ngày {grp.Key.Item2:dd/MM} (dạy Ca {string.Join(", Ca ", slotIndices.Select(idx => idx + 1))})");
                        flaggedTeachers.Add(grp.Key.Item1);
                    }
                }
                metrics.Add(new SoftMetricDto
                {
                    Name = "Giờ trống trong ngày dạy của giáo viên",
                    DisplayValue = $"{flaggedTeachers.Count} giáo viên có giờ trống giữa các ca dạy",
                    Status = flaggedTeachers.Count == 0 ? "Good" : flaggedTeachers.Count <= 2 ? "Warning" : "Bad",
                    Details = gapDetails
                });
            }

            return metrics;
        }

        // Descriptive stats (band distribution, class size, room capacity) for the overview UI —
        // same numbers a manual check would produce, computed once instead of by hand.
        private ScheduleDescriptiveStatsDto BuildDescriptiveStats(
            List<ClassDto> resultList,
            List<DraftClass> draftClasses,
            List<Teacher> teachers,
            List<Room> rooms)
        {
            var stats = new ScheduleDescriptiveStatsDto();
            if (!resultList.Any()) return stats;

            for (int i = 0; i < resultList.Count; i++)
            {
                var draft = draftClasses[i];
                // "NONE_REQUIRED" is a sentinel the frontend translates for display — real band
                // values (e.g. "IELTS 6.5") come from GetStringValue() and are language-neutral labels.
                var key = draft.RequiredGradeLevel.HasValue ? draft.RequiredGradeLevel.Value.GetStringValue() : "NONE_REQUIRED";
                stats.ClassesByGradeBand[key] = stats.ClassesByGradeBand.GetValueOrDefault(key) + 1;
            }

            var usedTeacherIds = resultList.Where(c => c.TeacherId.HasValue).Select(c => c.TeacherId!.Value).Distinct();
            foreach (var tid in usedTeacherIds)
            {
                var teacher = teachers.FirstOrDefault(t => t.Id == tid);
                var key = teacher?.GradeLevel.HasValue == true ? teacher.GradeLevel.Value.GetStringValue() : "UNSPECIFIED";
                stats.TeachersByGradeBandInUse[key] = stats.TeachersByGradeBandInUse.GetValueOrDefault(key) + 1;
            }

            stats.AvgClassSize = Math.Round(resultList.Average(c => c.StudentCount), MidpointRounding.AwayFromZero);

            var largest = resultList.OrderByDescending(c => c.StudentCount).First();
            var smallest = resultList.OrderBy(c => c.StudentCount).First();
            stats.LargestClass = new ClassSizeExtremeDto { ClassCode = largest.Code, ClassName = largest.Name, Size = largest.StudentCount };
            stats.SmallestClass = new ClassSizeExtremeDto { ClassCode = smallest.Code, ClassName = smallest.Name, Size = smallest.StudentCount };

            var usedRoomCapacities = resultList
                .Select(c => c.Schedules.FirstOrDefault()?.RoomId)
                .Where(id => id.HasValue)
                .Select(id => rooms.FirstOrDefault(r => r.Id == id!.Value)?.Capacity)
                .Where(cap => cap.HasValue)
                .Select(cap => cap!.Value)
                .ToList();
            if (usedRoomCapacities.Any())
            {
                stats.AvgRoomCapacity = Math.Round(usedRoomCapacities.Average(), 1);
            }

            var offlineFillRates = new List<double>();
            for (int i = 0; i < resultList.Count; i++)
            {
                if (draftClasses[i].EnrollType == 1) continue;
                var roomId = resultList[i].Schedules.FirstOrDefault()?.RoomId;
                var room = rooms.FirstOrDefault(r => r.Id == roomId);
                if (room?.Capacity is int cap && cap > 0)
                    offlineFillRates.Add(100.0 * resultList[i].StudentCount / cap);
            }
            if (offlineFillRates.Any())
            {
                stats.AvgRoomFillRatePercent = Math.Round(offlineFillRates.Average(), 1);
            }

            return stats;
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

                var semester = await _semesterRepository.GetByIdAsync(request.SemesterId);
                if (semester == null || semester.IsDeleted)
                    return ApiResponse<List<ClassDto>>.Fail("ERR_SEMESTER_NOT_FOUND", StatusCodes.Status404NotFound);

                using var transaction = await _classRepository.BeginTransactionAsync();
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

                        await _classRepository.AddAsync(entity);
                        await _classRepository.SaveChangesAsync(); // get Id

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
                        entity.TextSearch = StringHelper.GenerateTextSearch(entity.Code, entity.Name, entity.Description, entity.ScheduleDisplay);
                        await _classRepository.UpdateAsync(entity);

                        // Link students and mark registrations as Scheduled
                        foreach (var studentEntry in draftClass.Students)
                        {
                            await _studentClassRepository.AddAsync(new StudentClass
                            {
                                StudentId = studentEntry.StudentId,
                                ClassId = entity.Id,
                                EnrollDate = DateTime.UtcNow,
                                Status = (int)StudentClassStatus.Enrolled,
                                EnrollType = studentEntry.EnrollType
                            });

                            var reg = await _studentRegistrationRepository.FindAll(true)
                                .FirstOrDefaultAsync(r =>
                                    r.StudentId == studentEntry.StudentId &&
                                    r.CourseId == draftClass.CourseId &&
                                    r.SemesterId == request.SemesterId &&
                                    r.Status == (int)StudentRegistrationStatus.Pending);

                            if (reg != null)
                            {
                                reg.Status = (int)StudentRegistrationStatus.Scheduled;
                                await _studentRegistrationRepository.UpdateAsync(reg);
                            }
                        }

                        createdClasses.Add(entity);
                    }

                    await _classRepository.SaveChangesAsync();

                    // Upsert the auto-saved checkpoint: re-running auto-schedule for this semester
                    // overwrites the same "Original" snapshot rather than piling up duplicates.
                    var initialSnapshot = await BuildSnapshotAsync(semester.Id);
                    var autoVersion = await _scheduleVersionRepository.FindAll()
                        .FirstOrDefaultAsync(v => v.SemesterId == semester.Id && v.IsAutoSaved);
                    if (autoVersion != null)
                    {
                        autoVersion.ScheduleJson = JsonSerializer.Serialize(initialSnapshot);
                        await _scheduleVersionRepository.UpdateAsync(autoVersion);
                    }
                    else
                    {
                        await _scheduleVersionRepository.AddAsync(new ScheduleVersion
                        {
                            SemesterId = semester.Id,
                            Name = "Original",
                            ScheduleJson = JsonSerializer.Serialize(initialSnapshot),
                            IsAutoSaved = true
                        });
                    }
                    await _scheduleVersionRepository.SaveChangesAsync();

                    await transaction.CommitAsync();

                    // Reload and return persisted classes
                    var resultList = new List<ClassDto>();
                    foreach (var c in createdClasses)
                    {
                        var reloaded = await _classRepository.FindAll()
                            .Include(cl => cl.Course)
                            .Include(cl => cl.Teacher)
                            .Include(cl => cl.ClassSchedules).ThenInclude(cs => cs.TimeSlot)
                            .Include(cl => cl.ClassSchedules).ThenInclude(cs => cs.Room)
                            .Include(cl => cl.StudentClasses).ThenInclude(sc => sc.Student)
                            .FirstOrDefaultAsync(cl => cl.Id == c.Id);
                        if (reloaded != null)
                        {
                            resultList.Add(MapToDto(reloaded));
                            await _notificationService.SendClassCreatedNotificationAsync(reloaded);
                        }
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

        public async Task<ApiResponse<List<ClassDto>>> RollbackSemesterScheduleAsync(int semesterId, int versionId)
        {
            try
            {
                var semester = await _semesterRepository.GetByIdAsync(semesterId);
                if (semester == null || semester.IsDeleted)
                    return ApiResponse<List<ClassDto>>.Fail("ERR_SEMESTER_NOT_FOUND", StatusCodes.Status404NotFound);

                var version = await _scheduleVersionRepository.GetByIdAsync(versionId);
                if (version == null || version.IsDeleted)
                    return ApiResponse<List<ClassDto>>.Fail("ERR_SCHEDULE_VERSION_NOT_FOUND", StatusCodes.Status404NotFound);
                if (version.SemesterId != semesterId)
                    return ApiResponse<List<ClassDto>>.Fail("ERR_VERSION_SEMESTER_MISMATCH", StatusCodes.Status400BadRequest);

                using var transaction = await _classRepository.BeginTransactionAsync();
                try
                {
                    // 1. Fetch all classes in this semester with status Planning (0) and not deleted
                    var classesToDelete = await _classRepository.FindAll()
                        .Include(c => c.StudentClasses)
                        .Include(c => c.ClassSchedules)
                        .Where(c => c.SemesterId == semesterId && c.Status == (int)ClassStatus.Planning && !c.IsDeleted)
                        .ToListAsync();

                    var activeClassCodes = classesToDelete.Select(c => c.Code).ToHashSet();

                    if (classesToDelete.Any())
                    {
                        foreach (var c in classesToDelete)
                        {
                            // 2. Revert student registrations back to Pending
                            var studentIds = c.StudentClasses.Select(sc => sc.StudentId).ToList();
                            if (studentIds.Any())
                            {
                                var registrationsToRevert = await _studentRegistrationRepository.FindAll(true)
                                    .Where(r => r.SemesterId == semesterId && r.CourseId == c.CourseId && studentIds.Contains(r.StudentId))
                                    .ToListAsync();

                                foreach (var reg in registrationsToRevert)
                                {
                                    reg.Status = (int)StudentRegistrationStatus.Pending;
                                    await _studentRegistrationRepository.UpdateAsync(reg);
                                }
                            }

                            // 3. Clear student classes and schedules to ensure smooth cascading delete (if any manual tracking exists)
                            if (c.StudentClasses?.Any() == true)
                                await _studentClassRepository.DeleteRangeAsync(c.StudentClasses);
                            if (c.ClassSchedules?.Any() == true)
                                await _scheduleRepository.DeleteRangeAsync(c.ClassSchedules);

                            // 4. Delete the class itself
                            await _classRepository.DeleteAsync(c);
                        }
                        await _classRepository.SaveChangesAsync();
                    }

                    // 5. Deserialize the chosen version's snapshot
                    var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var snapshot = JsonSerializer.Deserialize<ScheduleVersionSnapshotDto>(version.ScheduleJson, jsonOpts);
                    if (snapshot == null || snapshot.Classes == null || !snapshot.Classes.Any())
                    {
                        return ApiResponse<List<ClassDto>>.Fail("ERR_CORRUPTED_BACKUP_DATA", StatusCodes.Status400BadRequest);
                    }

                    // 6. Re-create the classes using the chosen version's snapshot
                    var createdClasses = new List<Class>();
                    var processedCodes = new HashSet<string>();

                    foreach (var draftClass in snapshot.Classes)
                    {
                        if (string.IsNullOrWhiteSpace(draftClass.Code)) continue;
                        if (processedCodes.Contains(draftClass.Code)) continue;
                        processedCodes.Add(draftClass.Code);

                        // If the class is not in the active (Planning) class list prior to rollback, it's either
                        // explicitly deleted or has already progressed past Planning (Active/Completed/Cancelled) —
                        // rollback only ever touches Planning-status classes, so leave those alone rather than
                        // resurrecting or duplicating them.
                        if (!activeClassCodes.Contains(draftClass.Code))
                        {
                            var existing = await _classRepository.FindAll()
                                .IgnoreQueryFilters()
                                .FirstOrDefaultAsync(c => c.SemesterId == semesterId && c.Code == draftClass.Code);
                            if (existing != null && (existing.IsDeleted || existing.Status != (int)ClassStatus.Planning))
                            {
                                continue;
                            }
                        }
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

                        await _classRepository.AddAsync(entity);
                        await _classRepository.SaveChangesAsync(); // get Id

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
                        entity.TextSearch = StringHelper.GenerateTextSearch(entity.Code, entity.Name, entity.Description, entity.ScheduleDisplay);
                        await _classRepository.UpdateAsync(entity);

                        // Link students and mark registrations as Scheduled
                        foreach (var studentEntry in draftClass.Students)
                        {
                            await _studentClassRepository.AddAsync(new StudentClass
                            {
                                StudentId = studentEntry.StudentId,
                                ClassId = entity.Id,
                                EnrollDate = DateTime.UtcNow,
                                Status = (int)StudentClassStatus.Enrolled,
                                EnrollType = studentEntry.EnrollType
                            });

                            var reg = await _studentRegistrationRepository.FindAll(true)
                                .FirstOrDefaultAsync(r =>
                                    r.StudentId == studentEntry.StudentId &&
                                    r.CourseId == draftClass.CourseId &&
                                    r.SemesterId == semesterId &&
                                    r.Status == (int)StudentRegistrationStatus.Pending);

                            if (reg != null)
                            {
                                reg.Status = (int)StudentRegistrationStatus.Scheduled;
                                await _studentRegistrationRepository.UpdateAsync(reg);
                            }
                        }

                        createdClasses.Add(entity);
                    }

                    await _classRepository.SaveChangesAsync();
                    await transaction.CommitAsync();

                    // Reload and return recreated classes
                    var resultList = new List<ClassDto>();
                    foreach (var c in createdClasses)
                    {
                        var reloaded = await _classRepository.FindAll()
                            .Include(cl => cl.Course)
                            .Include(cl => cl.Teacher)
                            .Include(cl => cl.ClassSchedules).ThenInclude(cs => cs.TimeSlot)
                            .Include(cl => cl.ClassSchedules).ThenInclude(cs => cs.Room)
                            .Include(cl => cl.StudentClasses).ThenInclude(sc => sc.Student)
                            .FirstOrDefaultAsync(cl => cl.Id == c.Id);
                        if (reloaded != null) resultList.Add(MapToDto(reloaded));
                    }

                    return ApiResponse<List<ClassDto>>.Ok(resultList, "SCHEDULE_DRAFT_ROLLBACK_SUCCESS");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    throw new Exception("Error executing schedule rollback: " + ex.Message, ex);
                }
            }
            catch (Exception ex)
            {
                return ApiResponse<List<ClassDto>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<ScheduleVersionListItemDto>> SaveScheduleVersionAsync(int semesterId, string name)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                    return ApiResponse<ScheduleVersionListItemDto>.Fail("ERR_VERSION_NAME_REQUIRED", StatusCodes.Status400BadRequest);

                var semester = await _semesterRepository.GetByIdAsync(semesterId);
                if (semester == null || semester.IsDeleted)
                    return ApiResponse<ScheduleVersionListItemDto>.Fail("ERR_SEMESTER_NOT_FOUND", StatusCodes.Status404NotFound);

                var existingVersionCount = await _scheduleVersionRepository.FindAll()
                    .CountAsync(v => v.SemesterId == semesterId);
                if (existingVersionCount >= MaxScheduleVersionsPerSemester)
                    return ApiResponse<ScheduleVersionListItemDto>.Fail("ERR_MAX_SCHEDULE_VERSIONS_REACHED", StatusCodes.Status400BadRequest);

                var snapshot = await BuildSnapshotAsync(semesterId);
                if (!snapshot.Classes.Any())
                    return ApiResponse<ScheduleVersionListItemDto>.Fail("ERR_NO_SCHEDULE_TO_SAVE", StatusCodes.Status400BadRequest);

                var entity = new ScheduleVersion
                {
                    SemesterId = semesterId,
                    Name = name.Trim(),
                    ScheduleJson = JsonSerializer.Serialize(snapshot),
                    IsAutoSaved = false
                };
                await _scheduleVersionRepository.AddAsync(entity);
                await _scheduleVersionRepository.SaveChangesAsync();

                return ApiResponse<ScheduleVersionListItemDto>.Ok(new ScheduleVersionListItemDto
                {
                    Id = entity.Id,
                    Name = entity.Name,
                    CreatedAt = entity.CreatedAt,
                    CreatedBy = entity.CreatedBy,
                    ClassCount = snapshot.Classes.Count,
                    IsAutoSaved = entity.IsAutoSaved
                }, "SCHEDULE_VERSION_SAVED");
            }
            catch (Exception ex)
            {
                return ApiResponse<ScheduleVersionListItemDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<List<ScheduleVersionListItemDto>>> GetScheduleVersionsAsync(int semesterId)
        {
            try
            {
                var versions = await _scheduleVersionRepository.FindAll()
                    .Where(v => v.SemesterId == semesterId)
                    .OrderByDescending(v => v.CreatedAt)
                    .ToListAsync();

                var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var result = versions.Select(v =>
                {
                    var classCount = 0;
                    try
                    {
                        var snapshot = JsonSerializer.Deserialize<ScheduleVersionSnapshotDto>(v.ScheduleJson, jsonOpts);
                        classCount = snapshot?.Classes?.Count ?? 0;
                    }
                    catch (JsonException)
                    {
                        // Corrupted snapshot: report 0 rather than failing the whole list
                    }

                    return new ScheduleVersionListItemDto
                    {
                        Id = v.Id,
                        Name = v.Name,
                        CreatedAt = v.CreatedAt,
                        CreatedBy = v.CreatedBy,
                        ClassCount = classCount,
                        IsAutoSaved = v.IsAutoSaved
                    };
                }).ToList();

                return ApiResponse<List<ScheduleVersionListItemDto>>.Ok(result, "SCHEDULE_VERSIONS_FETCHED");
            }
            catch (Exception ex)
            {
                return ApiResponse<List<ScheduleVersionListItemDto>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<bool>> DeleteScheduleVersionAsync(int versionId)
        {
            try
            {
                var version = await _scheduleVersionRepository.GetByIdAsync(versionId);
                if (version == null || version.IsDeleted)
                    return ApiResponse<bool>.Fail("ERR_SCHEDULE_VERSION_NOT_FOUND", StatusCodes.Status404NotFound);

                if (version.IsAutoSaved)
                    return ApiResponse<bool>.Fail("ERR_CANNOT_DELETE_AUTO_VERSION", StatusCodes.Status400BadRequest);

                await _scheduleVersionRepository.DeleteAsync(version);
                await _scheduleVersionRepository.SaveChangesAsync();

                return ApiResponse<bool>.Ok(true, "SCHEDULE_VERSION_DELETED");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<List<ClassDto>>> GetScheduleVersionPreviewAsync(int versionId)
        {
            try
            {
                var version = await _scheduleVersionRepository.GetByIdAsync(versionId);
                if (version == null || version.IsDeleted)
                    return ApiResponse<List<ClassDto>>.Fail("ERR_SCHEDULE_VERSION_NOT_FOUND", StatusCodes.Status404NotFound);

                var semester = await _semesterRepository.GetByIdAsync(version.SemesterId);
                if (semester == null || semester.IsDeleted)
                    return ApiResponse<List<ClassDto>>.Fail("ERR_SEMESTER_NOT_FOUND", StatusCodes.Status404NotFound);

                var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                ScheduleVersionSnapshotDto? snapshot;
                try
                {
                    snapshot = JsonSerializer.Deserialize<ScheduleVersionSnapshotDto>(version.ScheduleJson, jsonOpts);
                }
                catch (JsonException)
                {
                    return ApiResponse<List<ClassDto>>.Fail("ERR_CORRUPTED_BACKUP_DATA", StatusCodes.Status400BadRequest);
                }
                if (snapshot == null || snapshot.Classes == null || !snapshot.Classes.Any())
                    return ApiResponse<List<ClassDto>>.Fail("ERR_CORRUPTED_BACKUP_DATA", StatusCodes.Status400BadRequest);

                var teacherIds = snapshot.Classes.Select(c => c.TeacherId).Distinct().ToList();
                var teachers = await _teacherRepository.FindAll()
                    .Where(t => teacherIds.Contains(t.Id))
                    .ToDictionaryAsync(t => t.Id, t => t);

                var roomIds = snapshot.Classes
                    .SelectMany(c => c.WeeklySchedules)
                    .Where(w => w.RoomId.HasValue)
                    .Select(w => w.RoomId!.Value)
                    .Distinct()
                    .ToList();
                var rooms = await _roomRepository.FindAll()
                    .Where(r => roomIds.Contains(r.Id))
                    .ToDictionaryAsync(r => r.Id, r => r);

                var resultList = new List<ClassDto>();
                foreach (var cls in snapshot.Classes)
                {
                    teachers.TryGetValue(cls.TeacherId, out var teacher);
                    var orderedWS = cls.WeeklySchedules.OrderBy(w => w.DayOfWeek).ToList();
                    var inMemorySchedules = new List<ClassScheduleDto>();
                    int lessonNo = 1;
                    var cur = semester.StartDate;
                    while (cur <= semester.EndDate)
                    {
                        var match = orderedWS.FirstOrDefault(w => (int)cur.DayOfWeek == w.DayOfWeek);
                        if (match != null && TimeSpan.TryParse(match.StartTime, out var st) && TimeSpan.TryParse(match.EndTime, out _))
                        {
                            var room = match.RoomId.HasValue && rooms.TryGetValue(match.RoomId.Value, out var r) ? r : null;
                            var fixedSlot = FixedTimeSlot.FromStartTime(st);
                            inMemorySchedules.Add(new ClassScheduleDto
                            {
                                LessonNo = lessonNo,
                                ScheduleDate = cur,
                                StartTime = match.StartTime,
                                EndTime = match.EndTime,
                                RoomId = match.RoomId,
                                RoomName = room?.Name,
                                TeacherId = cls.TeacherId,
                                TeacherName = teacher?.Name,
                                SlotName = fixedSlot?.Name,
                                Status = (int)ClassScheduleStatus.Scheduled,
                                Code = $"SCH_PREVIEW_{cls.Code}_{lessonNo}",
                                Name = $"Buổi học {lessonNo}"
                            });
                            lessonNo++;
                        }
                        cur = cur.AddDays(1);
                    }

                    resultList.Add(new ClassDto
                    {
                        Id = 0, // 0 signals preview (not persisted)
                        Code = cls.Code,
                        Name = cls.Name,
                        Status = (int)ClassStatus.Planning,
                        StatusName = "Planning",
                        Type = cls.EnrollType,
                        TypeName = cls.EnrollType == 1 ? "Online" : "Offline",
                        StartDate = semester.StartDate,
                        EndDate = semester.EndDate,
                        CourseId = cls.CourseId,
                        TeacherId = cls.TeacherId,
                        TeacherName = teacher?.Name,
                        SemesterId = semester.Id,
                        SemesterName = semester.Name,
                        ExpectedLessons = lessonNo - 1,
                        WeeklySchedulesJson = JsonSerializer.Serialize(cls.WeeklySchedules,
                            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                        StudentCount = cls.Students.Count,
                        Schedules = inMemorySchedules
                    });
                }

                return ApiResponse<List<ClassDto>>.Ok(resultList, "SCHEDULE_VERSION_PREVIEW_GENERATED");
            }
            catch (Exception ex)
            {
                return ApiResponse<List<ClassDto>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task PurgeScheduleVersionsIfSemesterEmptyAsync(int semesterId)
        {
            var hasRemainingPlanningClasses = await _classRepository.FindAll()
                .AnyAsync(c => c.SemesterId == semesterId && c.Status == (int)ClassStatus.Planning && !c.IsDeleted);
            if (hasRemainingPlanningClasses)
                return;

            var versions = await _scheduleVersionRepository.FindAll()
                .Where(v => v.SemesterId == semesterId)
                .ToListAsync();
            if (!versions.Any())
                return;

            foreach (var version in versions)
            {
                await _scheduleVersionRepository.DeleteAsync(version);
            }
            await _scheduleVersionRepository.SaveChangesAsync();
        }

        // ================= PRIVATE HELPERS =================

        private async Task<ScheduleVersionSnapshotDto> BuildSnapshotAsync(int semesterId)
        {
            var classes = await _classRepository.FindAll()
                .Include(c => c.StudentClasses)
                .Where(c => c.SemesterId == semesterId && c.Status == (int)ClassStatus.Planning && !c.IsDeleted)
                .ToListAsync();

            var snapshot = new ScheduleVersionSnapshotDto { SemesterId = semesterId };
            var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            foreach (var c in classes)
            {
                List<WeeklyScheduleDto> weekly = new();
                if (!string.IsNullOrEmpty(c.WeeklySchedulesJson))
                {
                    weekly = JsonSerializer.Deserialize<List<WeeklyScheduleDto>>(c.WeeklySchedulesJson, jsonOpts) ?? new();
                }

                snapshot.Classes.Add(new ClassDraftSaveDto
                {
                    Code = c.Code,
                    Name = c.Name,
                    CourseId = c.CourseId ?? 0,
                    TeacherId = c.TeacherId ?? 0,
                    EnrollType = c.Type,
                    ExpectedLessons = c.ExpectedLessons ?? 30,
                    WeeklySchedules = weekly,
                    Students = c.StudentClasses.Select(sc => new StudentEnrollDto
                    {
                        StudentId = sc.StudentId,
                        EnrollType = sc.EnrollType ?? 0
                    }).ToList()
                });
            }

            return snapshot;
        }

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

            // Clear the existing navigation collection to replace old schedules
            entity.ClassSchedules.Clear();

            // Cache: load existing DB time slots once, keyed by (StartTime, EndTime)
            var dbTimeSlotCache = await _timeSlotRepository.FindAll()
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
                            await _timeSlotRepository.AddAsync(timeSlot);
                            await _timeSlotRepository.SaveChangesAsync();
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
                            await _timeSlotRepository.AddAsync(timeSlot);
                            await _timeSlotRepository.SaveChangesAsync();
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

        // Returns coded reasons (not pre-formatted sentences) so the frontend can localize each one via
        // backendMessages.infeasibilityReasons.<Code> with the given Params interpolated in.
        private List<InfeasibilityReasonDto> DiagnoseInfeasibility(
            List<DraftClass> draftClasses,
            List<Teacher> teachers,
            List<Room> rooms,
            Dictionary<int, HashSet<(int, int)>> teacherAvailMap,
            int[] allowedDays,
            int[] globalAllowedSlots,
            int freq,
            int numFixed)
        {
            var reasons = new List<InfeasibilityReasonDto>();

            // Check room capacity for each draft class
            foreach (var draft in draftClasses)
            {
                var suitableRooms = rooms.Where(r => (r.Capacity ?? int.MaxValue) >= draft.Size).ToList();
                if (!suitableRooms.Any())
                {
                    reasons.Add(new InfeasibilityReasonDto
                    {
                        Code = "ROOM_CAPACITY_INSUFFICIENT",
                        Params = new Dictionary<string, string>
                        {
                            ["courseName"] = draft.CourseName,
                            ["classSize"] = draft.Size.ToString(),
                            ["maxRoomCapacity"] = rooms.Max(r => r.Capacity ?? 0).ToString()
                        }
                    });
                }
            }

            // Check teacher availability for each class
            foreach (var draft in draftClasses)
            {
                var preferredSlot = draft.PreferredSlotIndex;
                var classAllowedDaysList = new List<int>();
                for (int d = 0; d < 7; d++)
                {
                    if ((draft.PreferredDaysOfWeek & (1 << d)) != 0)
                    {
                        classAllowedDaysList.Add(d);
                    }
                }
                var classAllowedDays = classAllowedDaysList.Intersect(allowedDays).ToArray();
                if (!classAllowedDays.Any()) classAllowedDays = allowedDays;

                bool hasTeacher = false;
                foreach (var t in teachers)
                {
                    if (!teacherAvailMap.ContainsKey(t.Id))
                    {
                        hasTeacher = true;
                        break;
                    }
                    var active = teacherAvailMap[t.Id];
                    var matchingSlots = active.Where(slot => classAllowedDays.Contains(slot.Item1) && slot.Item2 == preferredSlot);
                    if (matchingSlots.Any())
                    {
                        hasTeacher = true;
                        break;
                    }
                }

                if (!hasTeacher)
                {
                    reasons.Add(new InfeasibilityReasonDto
                    {
                        Code = "TEACHER_NO_AVAILABILITY",
                        Params = new Dictionary<string, string>
                        {
                            ["courseName"] = draft.CourseName,
                            ["slotNumber"] = (preferredSlot + 1).ToString(),
                            ["days"] = string.Join(", ", classAllowedDays.Select(GetDayOfWeekName))
                        }
                    });
                }
            }

            // Check total room-slot capacity
            int totalSessions = draftClasses.Count * freq;
            int totalRoomSlots = allowedDays.Length * globalAllowedSlots.Length * rooms.Count;
            if (totalSessions > totalRoomSlots)
            {
                reasons.Add(new InfeasibilityReasonDto
                {
                    Code = "ROOM_SLOT_CAPACITY_EXCEEDED",
                    Params = new Dictionary<string, string>
                    {
                        ["totalSessions"] = totalSessions.ToString(),
                        ["totalRoomSlots"] = totalRoomSlots.ToString()
                    }
                });
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
                reasons.Add(new InfeasibilityReasonDto
                {
                    Code = "TEACHER_SLOT_CAPACITY_INSUFFICIENT",
                    Params = new Dictionary<string, string>
                    {
                        ["totalTeacherSlots"] = totalTeacherSlots.ToString(),
                        ["totalSessions"] = totalSessions.ToString()
                    }
                });
            }

            if (reasons.Any())
            {
                return reasons;
            }

            return new List<InfeasibilityReasonDto> { new() { Code = "GENERIC_INFEASIBLE" } };
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

