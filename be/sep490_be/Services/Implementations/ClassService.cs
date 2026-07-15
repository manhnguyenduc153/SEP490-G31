using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Mapster;
using sep490_be.DTO;
using sep490_be.DTO.Class;
using sep490_be.Models;
using sep490_be.Repositories.Interfaces;
using sep490_be.Services.Interfaces;
using sep490_be.Helpers;
using sep490_be.Enums;
using sep490_be.Repositories.Common;
using Microsoft.AspNetCore.Identity;

namespace sep490_be.Services.Implementations
{
    public class ClassService : IClassService
    {
        private readonly IClassRepository _repository;
        private readonly ICourseRepository _courseRepository;
        private readonly ITeacherRepository _teacherRepository;
        private readonly IStudentRepository _studentRepository;
        private readonly IBaseRepository<TimeSlot, ApplicationDbContext> _timeSlotRepository;
        private readonly IBaseRepository<ClassSchedule, ApplicationDbContext> _scheduleRepository;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _dbContext;
        private readonly IScheduleOptimizationService _optService;
 
        public ClassService(
            IClassRepository repository,
            ICourseRepository courseRepository,
            ITeacherRepository teacherRepository,
            IStudentRepository studentRepository,
            IBaseRepository<TimeSlot, ApplicationDbContext> timeSlotRepository,
            IBaseRepository<ClassSchedule, ApplicationDbContext> scheduleRepository,
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext dbContext,
            IScheduleOptimizationService optService)
        {
            _repository = repository;
            _courseRepository = courseRepository;
            _teacherRepository = teacherRepository;
            _studentRepository = studentRepository;
            _timeSlotRepository = timeSlotRepository;
            _scheduleRepository = scheduleRepository;
            _userManager = userManager;
            _roleManager = roleManager;
            _dbContext = dbContext;
            _optService = optService;
        }

        public async Task<ApiResponse<PagingResponse<ClassDto>>> GetAllAsync(ClassSearchDto searchDto)
        {
            try
            {
                await AutoUpdateClassStatusesAsync();
                var query = _repository.FindAll()
                    .Include(c => c.Course)
                    .Include(c => c.Teacher)
                    .Include(c => c.Semester)
                    .Include(c => c.StudentClasses)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(searchDto.Keyword))
                {
                    query = query.Where(c => c.TextSearch != null && c.TextSearch.Contains(searchDto.Keyword));
                }

                if (searchDto.CourseId.HasValue)
                {
                    query = query.Where(c => c.CourseId == searchDto.CourseId.Value);
                }

                if (searchDto.TeacherId.HasValue)
                {
                    query = query.Where(c => c.TeacherId == searchDto.TeacherId.Value);
                }

                var totalRecords = await query.CountAsync();
                var entities = await query.ApplyPagingAsync(searchDto);

                var dtos = entities.Select(MapToDto);
                var pagingResponse = dtos.ToPagingResponse(totalRecords, searchDto);

                return ApiResponse<PagingResponse<ClassDto>>.Ok(pagingResponse, "GET_CLASS_LIST_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<PagingResponse<ClassDto>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<ClassDto>> GetByIdAsync(int id, string? username = null)
        {
            try
            {
                await AutoUpdateClassStatusesAsync();
                var entity = await _repository.FindAll()
                    .Include(c => c.Course)
                    .Include(c => c.Teacher)
                    .Include(c => c.Semester)
                    .Include(c => c.StudentClasses)
                        .ThenInclude(sc => sc.Student)
                    .Include(c => c.ClassSchedules)
                        .ThenInclude(cs => cs.TimeSlot)
                    .Include(c => c.ClassSchedules)
                        .ThenInclude(cs => cs.Room)
                    .Include(c => c.ClassSchedules)
                        .ThenInclude(cs => cs.Teacher)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (entity == null)
                {
                    return ApiResponse<ClassDto>.Fail("ERR_CLASS_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                if (!string.IsNullOrEmpty(username))
                {
                    var user = await _userManager.FindByNameAsync(username);
                    if (user != null)
                    {
                        var roles = await _userManager.GetRolesAsync(user);
                        if (!roles.Contains("Admin"))
                        {
                            if (roles.Contains("Teacher"))
                            {
                                var teacher = await _teacherRepository.FindAll()
                                    .FirstOrDefaultAsync(t => t.Email == user.Email || t.Email == username);
                                if (teacher == null || entity.TeacherId != teacher.Id)
                                {
                                    return ApiResponse<ClassDto>.Fail("ERR_FORBIDDEN", StatusCodes.Status403Forbidden);
                                }
                            }
                            else if (roles.Contains("Student"))
                            {
                                var student = await _studentRepository.FindAll()
                                    .FirstOrDefaultAsync(s => s.Email == user.Email || s.Email == username);
                                if (student == null || !entity.StudentClasses.Any(sc => sc.StudentId == student.Id))
                                {
                                    return ApiResponse<ClassDto>.Fail("ERR_FORBIDDEN", StatusCodes.Status403Forbidden);
                                }
                            }
                            else
                            {
                                return ApiResponse<ClassDto>.Fail("ERR_FORBIDDEN", StatusCodes.Status403Forbidden);
                            }
                        }
                    }
                }

                return ApiResponse<ClassDto>.Ok(MapToDto(entity), "GET_CLASS_DETAIL_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<ClassDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<ClassDto>> CreateAsync(ClassSaveDto dto)
        {
            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                await ProcessNewStudentsAsync(dto);
                await ProcessNewTeacherAsync(dto);
                await ProcessNewCourseAsync(dto);

                var validationError = await ValidateAsync(dto, isEdit: false);
                if (validationError != null)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<ClassDto>.Fail(validationError, StatusCodes.Status400BadRequest);
                }

                var entity = dto.Adapt<Class>();
                entity.Id = 0;
                entity.StudentClasses = new List<StudentClass>();

                if (dto.StudentIds != null && dto.StudentIds.Any())
                {
                    foreach (var studentId in dto.StudentIds)
                    {
                        entity.StudentClasses.Add(new StudentClass
                        {
                            StudentId = studentId,
                            EnrollDate = DateTime.UtcNow,
                            Status = (int)StudentClassStatus.Enrolled
                        });
                    }
                }

                await GenerateSchedulesAsync(entity, dto);

                await _repository.AddAsync(entity);
                await _repository.SaveChangesAsync();

                await transaction.CommitAsync();

                // Reload to populate relationships for return value
                var createdClass = await _repository.FindAll()
                    .Include(c => c.Course)
                    .Include(c => c.Teacher)
                    .Include(c => c.Semester)
                    .Include(c => c.StudentClasses)
                    .FirstOrDefaultAsync(c => c.Id == entity.Id);

                return ApiResponse<ClassDto>.Created(MapToDto(createdClass ?? entity), "CREATE_CLASS_SUCCESS");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return ApiResponse<ClassDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<ClassDto>> EditAsync(ClassSaveDto dto)
        {
            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                await ProcessNewStudentsAsync(dto);
                await ProcessNewTeacherAsync(dto);
                await ProcessNewCourseAsync(dto);

                var validationError = await ValidateAsync(dto, isEdit: true);
                if (validationError != null)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<ClassDto>.Fail(validationError, StatusCodes.Status400BadRequest);
                }

                var existingEntity = await _repository.FindAll(trackChanges: true)
                    .Include(c => c.StudentClasses)
                    .Include(c => c.ClassSchedules)
                    .FirstOrDefaultAsync(c => c.Id == dto.Id);

                if (existingEntity == null)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<ClassDto>.Fail("ERR_CLASS_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                // Delete old schedules first to avoid orphans
                if (existingEntity.ClassSchedules != null && existingEntity.ClassSchedules.Any())
                {
                    await _scheduleRepository.DeleteRangeAsync(existingEntity.ClassSchedules.ToList());
                }

                // Sync StudentClasses
                var currentStudentIds = existingEntity.StudentClasses.Select(sc => sc.StudentId).ToList();
                var newStudentIds = dto.StudentIds ?? new List<int>();

                // Remove students that are no longer assigned
                var studentsToRemove = existingEntity.StudentClasses.Where(sc => !newStudentIds.Contains(sc.StudentId)).ToList();
                foreach (var sc in studentsToRemove)
                {
                    existingEntity.StudentClasses.Remove(sc);
                }

                // Add new students
                var studentsToAdd = newStudentIds.Where(id => !currentStudentIds.Contains(id)).ToList();
                foreach (var studentId in studentsToAdd)
                {
                    existingEntity.StudentClasses.Add(new StudentClass
                    {
                        StudentId = studentId,
                        EnrollDate = DateTime.UtcNow,
                        Status = (int)StudentClassStatus.Enrolled
                    });
                }

                // Map basic fields
                dto.Adapt(existingEntity);

                // Re-generate schedules
                await GenerateSchedulesAsync(existingEntity, dto);

                await _repository.SaveChangesAsync();

                await transaction.CommitAsync();

                // Reload to populate relationships for return value
                var updatedClass = await _repository.FindAll()
                    .Include(c => c.Course)
                    .Include(c => c.Teacher)
                    .Include(c => c.Semester)
                    .Include(c => c.StudentClasses)
                    .FirstOrDefaultAsync(c => c.Id == existingEntity.Id);

                return ApiResponse<ClassDto>.Ok(MapToDto(updatedClass ?? existingEntity), "UPDATE_CLASS_SUCCESS");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return ApiResponse<ClassDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<bool>> DeleteAsync(int id)
        {
            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                var existingEntity = await _repository.GetByIdAsync(id);
                if (existingEntity == null)
                {
                    return ApiResponse<bool>.Fail("ERR_CLASS_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                // 1. Get student IDs enrolled in this class
                var studentClasses = await _dbContext.StudentClasses
                    .Where(sc => sc.ClassId == id)
                    .ToListAsync();
                var studentIds = studentClasses.Select(sc => sc.StudentId).ToList();

                if (studentIds.Any() && existingEntity.CourseId.HasValue)
                {
                    // 2. Find the semester corresponding to this class (based on Class StartDate matching Semester StartDate)
                    var semester = await _dbContext.Semesters
                        .FirstOrDefaultAsync(s => !s.IsDeleted && s.StartDate == existingEntity.StartDate);

                    if (semester != null)
                    {
                        // 3. Find registrations for these students, for this course, in this semester, that are Scheduled (2)
                        var regsToReset = await _dbContext.StudentRegistrations
                            .Where(r => r.SemesterId == semester.Id 
                                     && r.CourseId == existingEntity.CourseId 
                                     && r.Status == (int)StudentRegistrationStatus.Scheduled 
                                     && studentIds.Contains(r.StudentId))
                            .ToListAsync();

                        foreach (var reg in regsToReset)
                        {
                            reg.Status = (int)StudentRegistrationStatus.Pending;
                            _dbContext.StudentRegistrations.Update(reg);
                        }
                    }
                    else
                    {
                        // Fallback: Reset any Scheduled registrations for this course and these students
                        var regsToReset = await _dbContext.StudentRegistrations
                            .Where(r => r.CourseId == existingEntity.CourseId 
                                     && r.Status == (int)StudentRegistrationStatus.Scheduled 
                                     && studentIds.Contains(r.StudentId))
                            .ToListAsync();

                        foreach (var reg in regsToReset)
                        {
                            reg.Status = (int)StudentRegistrationStatus.Pending;
                            _dbContext.StudentRegistrations.Update(reg);
                        }
                    }
                }

                // Remove StudentClasses relations to avoid orphans
                if (studentClasses.Any())
                {
                    _dbContext.StudentClasses.RemoveRange(studentClasses);
                }

                // Delete the class itself
                await _repository.DeleteAsync(existingEntity);
                await _dbContext.SaveChangesAsync();
                
                await transaction.CommitAsync();

                return ApiResponse<bool>.Ok(true, "DELETE_CLASS_SUCCESS");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return ApiResponse<bool>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<bool>> DeactiveAsync(int id)
        {
            try
            {
                var existingEntity = await _repository.GetByIdAsync(id);
                if (existingEntity == null)
                {
                    return ApiResponse<bool>.Fail("ERR_CLASS_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                await _repository.DeactiveAsync(existingEntity);
                await _repository.SaveChangesAsync();

                return ApiResponse<bool>.Ok(true, "DEACTIVATE_CLASS_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<PagingResponse<ClassDto>>> GetTeacherClassesAsync(string username, ClassSearchDto searchDto)
        {
            try
            {
                await AutoUpdateClassStatusesAsync();

                var user = await _userManager.FindByNameAsync(username);
                if (user == null)
                {
                    return ApiResponse<PagingResponse<ClassDto>>.Fail("ERR_USER_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                var teacher = await _teacherRepository.FindAll()
                    .FirstOrDefaultAsync(t => t.Email == user.Email || t.Email == username);
                if (teacher == null)
                {
                    return ApiResponse<PagingResponse<ClassDto>>.Fail("ERR_TEACHER_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                var query = _repository.FindAll()
                    .Include(c => c.Course)
                    .Include(c => c.Teacher)
                    .Include(c => c.Semester)
                    .Include(c => c.StudentClasses)
                    .Where(c => c.TeacherId == teacher.Id)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(searchDto.Keyword))
                {
                    query = query.Where(c => c.TextSearch != null && c.TextSearch.Contains(searchDto.Keyword));
                }

                if (searchDto.CourseId.HasValue)
                {
                    query = query.Where(c => c.CourseId == searchDto.CourseId.Value);
                }

                var totalRecords = await query.CountAsync();
                var entities = await query.ApplyPagingAsync(searchDto);

                var dtos = entities.Select(MapToDto);
                var pagingResponse = dtos.ToPagingResponse(totalRecords, searchDto);

                return ApiResponse<PagingResponse<ClassDto>>.Ok(pagingResponse, "GET_TEACHER_CLASSES_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<PagingResponse<ClassDto>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<PagingResponse<ClassDto>>> GetStudentClassesAsync(string username, ClassSearchDto searchDto)
        {
            try
            {
                await AutoUpdateClassStatusesAsync();

                var user = await _userManager.FindByNameAsync(username);
                if (user == null)
                {
                    return ApiResponse<PagingResponse<ClassDto>>.Fail("ERR_USER_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                var student = await _studentRepository.FindAll()
                    .Include(s => s.StudentClasses)
                    .FirstOrDefaultAsync(s => s.Email == user.Email || s.Email == username);
                if (student == null)
                {
                    return ApiResponse<PagingResponse<ClassDto>>.Fail("ERR_STUDENT_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                var classIds = student.StudentClasses
                    .Where(sc => sc.Status == (int)StudentClassStatus.Enrolled || sc.Status == (int)StudentClassStatus.Studying || sc.Status == (int)StudentClassStatus.Completed)
                    .Select(sc => sc.ClassId)
                    .ToList();

                var query = _repository.FindAll()
                    .Include(c => c.Course)
                    .Include(c => c.Teacher)
                    .Include(c => c.Semester)
                    .Include(c => c.StudentClasses)
                    .Where(c => classIds.Contains(c.Id))
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(searchDto.Keyword))
                {
                    query = query.Where(c => c.TextSearch != null && c.TextSearch.Contains(searchDto.Keyword));
                }

                if (searchDto.CourseId.HasValue)
                {
                    query = query.Where(c => c.CourseId == searchDto.CourseId.Value);
                }

                var totalRecords = await query.CountAsync();
                var entities = await query.ApplyPagingAsync(searchDto);

                var dtos = entities.Select(MapToDto);
                var pagingResponse = dtos.ToPagingResponse(totalRecords, searchDto);

                return ApiResponse<PagingResponse<ClassDto>>.Ok(pagingResponse, "GET_STUDENT_CLASSES_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<PagingResponse<ClassDto>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        // ===================== PRIVATE MAPPING =====================

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
                Note = cs.Note
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

        // ===================== PRIVATE VALIDATE =====================

        private async Task<string?> ValidateAsync(ClassSaveDto dto, bool isEdit)
        {
            if (string.IsNullOrWhiteSpace(dto.Code))
                return "ERR_CODE_EMPTY";

            if (dto.Code.Length > 50)
                return "ERR_CODE_MAX_LENGTH";

            if (string.IsNullOrWhiteSpace(dto.Name))
                return "ERR_NAME_EMPTY";

            if (dto.Name.Length > 200)
                return "ERR_NAME_MAX_LENGTH";

            if (!dto.StartDate.HasValue)
                return "ERR_START_DATE_EMPTY";

            if ((!dto.SemesterId.HasValue || dto.SemesterId.Value <= 0) && (!dto.ExpectedLessons.HasValue || dto.ExpectedLessons.Value <= 0))
                return "ERR_EXPECTED_LESSONS_INVALID";

            if (dto.Description != null && dto.Description.Length > 1000)
                return "ERR_DESC_MAX_LENGTH";

            if (dto.ScheduleDisplay != null && dto.ScheduleDisplay.Length > 200)
                return "ERR_SCHEDULE_MAX_LENGTH";

            // Kiểm tra trùng mã
            var duplicateCode = await _repository.FindAll()
                .FirstOrDefaultAsync(c => c.Code == dto.Code && (!isEdit || c.Id != dto.Id));

            if (duplicateCode != null)
                return "ERR_CODE_DUPLICATE";

            // Kiểm tra trùng tên
            var duplicateName = await _repository.FindAll()
                .FirstOrDefaultAsync(c => c.Name == dto.Name && (!isEdit || c.Id != dto.Id));

            if (duplicateName != null)
                return "ERR_NAME_DUPLICATE";

            // Kiểm tra khóa học có tồn tại
            if (dto.CourseId.HasValue)
            {
                var courseExists = await _courseRepository.ExistsAsync(c => c.Id == dto.CourseId.Value);
                if (!courseExists)
                    return "ERR_COURSE_NOT_FOUND";
            }

            // Kiểm tra giáo viên có tồn tại
            if (dto.TeacherId.HasValue)
            {
                var teacherExists = await _teacherRepository.ExistsAsync(t => t.Id == dto.TeacherId.Value);
                if (!teacherExists)
                    return "ERR_TEACHER_NOT_FOUND";
            }

            // Kiểm tra học sinh có tồn tại
            if (dto.StudentIds != null && dto.StudentIds.Any())
            {
                var existingStudentCount = await _studentRepository.FindAll()
                    .CountAsync(s => dto.StudentIds.Contains(s.Id));
                if (existingStudentCount != dto.StudentIds.Count)
                    return "ERR_STUDENT_NOT_FOUND";
            }

            // Kiểm tra sức chứa phòng học so với số lượng học sinh
            int studentCount = dto.StudentIds?.Count ?? 0;
            if (dto.WeeklySchedules != null && dto.WeeklySchedules.Any())
            {
                var roomIds = dto.WeeklySchedules
                    .Where(w => w.RoomId.HasValue)
                    .Select(w => w.RoomId.Value)
                    .Distinct()
                    .ToList();

                if (roomIds.Any())
                {
                    var rooms = await _dbContext.Rooms
                        .Where(r => roomIds.Contains(r.Id))
                        .ToListAsync();

                    foreach (var room in rooms)
                    {
                        if (room.Capacity.HasValue && studentCount > room.Capacity.Value)
                        {
                            return $"ERR_ROOM_CAPACITY_EXCEEDED_{room.Name}";
                        }
                    }
                }
            }

            // Kiểm tra trùng lịch học của học sinh (1 học sinh chỉ được học 1 lớp tại 1 thời điểm)
            if (dto.StudentIds != null && dto.StudentIds.Any() && dto.StartDate.HasValue && dto.ExpectedLessons.HasValue && dto.ExpectedLessons.Value > 0 && dto.WeeklySchedules != null && dto.WeeklySchedules.Any())
            {
                var currentDate = dto.StartDate.Value;
                int lessonNo = 1;
                var weeklySchedules = dto.WeeklySchedules.OrderBy(w => w.DayOfWeek).ToList();
                DateTime? proposedEndDate = null;

                if (!weeklySchedules.Any(w => w.DayOfWeek < 0 || w.DayOfWeek > 6))
                {
                    while (lessonNo <= dto.ExpectedLessons.Value)
                    {
                        var match = weeklySchedules.FirstOrDefault(w => (int)currentDate.DayOfWeek == w.DayOfWeek);
                        if (match != null)
                        {
                            if (lessonNo == dto.ExpectedLessons.Value)
                            {
                                proposedEndDate = currentDate;
                            }
                            lessonNo++;
                        }
                        currentDate = currentDate.AddDays(1);
                    }

                    if (proposedEndDate.HasValue)
                    {
                        var proposedStartDate = dto.StartDate.Value;

                        var otherStudentClasses = await _dbContext.StudentClasses
                            .Include(sc => sc.Student)
                            .Include(sc => sc.Class)
                            .Where(sc => dto.StudentIds.Contains(sc.StudentId)
                                      && sc.ClassId != dto.Id
                                      && sc.Class != null
                                      && !sc.Class.IsDeleted
                                      && (sc.Status == (int)StudentClassStatus.Enrolled || sc.Status == (int)StudentClassStatus.Studying))
                            .ToListAsync();

                        if (otherStudentClasses.Any())
                        {
                            var otherClassIds = otherStudentClasses.Select(sc => sc.ClassId).Distinct().ToList();

                            var conflictingSchedules = await _dbContext.ClassSchedules
                                .Where(cs => cs.ClassId.HasValue
                                          && otherClassIds.Contains(cs.ClassId.Value)
                                          && cs.Class != null
                                          && !cs.Class.IsDeleted
                                          && cs.ScheduleDate >= proposedStartDate
                                          && cs.ScheduleDate <= proposedEndDate)
                                .ToListAsync();

                            if (conflictingSchedules.Any())
                            {
                                var conflictingEmails = new List<string>();
                                foreach (var sc in otherStudentClasses)
                                {
                                    var hasConflict = conflictingSchedules.Any(cs => cs.ClassId == sc.ClassId);
                                    if (hasConflict && sc.Student != null && !string.IsNullOrWhiteSpace(sc.Student.Email))
                                    {
                                        conflictingEmails.Add(sc.Student.Email.Trim());
                                    }
                                }

                                if (conflictingEmails.Any())
                                {
                                    var uniqueEmails = conflictingEmails.Distinct().ToList();
                                    return $"ERR_STUDENT_CONFLICT_{uniqueEmails.Count}__{string.Join(",", uniqueEmails)}";
                                }
                            }
                        }
                    }
                }
            }

            // Kiểm tra trùng lịch dạy của giáo viên hoặc phòng học
            var conflictCheck = await _optService.CheckConflictAsync(dto);
            if (conflictCheck.Success && conflictCheck.Data != null && conflictCheck.Data.HasConflict)
            {
                var firstConflict = conflictCheck.Data.Conflicts.First();
                if (firstConflict.Type == "Teacher")
                {
                    return $"ERR_TEACHER_CONFLICT_{firstConflict.ConflictClassCode}";
                }
                else
                {
                    return $"ERR_ROOM_CONFLICT_{firstConflict.ConflictClassCode}";
                }
            }
 
            return null;
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

        private async Task GenerateSchedulesAsync(Class entity, ClassSaveDto dto)
        {
            if (dto.WeeklySchedules == null || !dto.WeeklySchedules.Any() || !dto.StartDate.HasValue)
            {
                return;
            }

            // Format ScheduleDisplay
            entity.ScheduleDisplay = string.Join(", ", dto.WeeklySchedules
                .OrderBy(w => w.DayOfWeek)
                .Select(w => $"{GetDayOfWeekName(w.DayOfWeek)} {w.StartTime}-{w.EndTime}"));

            // Serialize WeeklySchedulesJson with CamelCase naming policy
            var jsonOptions = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            };
            entity.WeeklySchedulesJson = System.Text.Json.JsonSerializer.Serialize(dto.WeeklySchedules, jsonOptions);
            entity.AutoRefund = dto.AutoRefund;
            entity.SemesterId = dto.SemesterId;

            // Clear the existing navigation collection
            entity.ClassSchedules.Clear();

            // Generate dates
            var currentDate = dto.StartDate.Value;
            var endDate = dto.EndDate;
            int lessonNo = 1;
            var weeklySchedules = dto.WeeklySchedules.OrderBy(w => w.DayOfWeek).ToList();

            // Guard against infinite loops if the DayOfWeek values are invalid
            if (weeklySchedules.Any(w => w.DayOfWeek < 0 || w.DayOfWeek > 6))
            {
                return;
            }

            if (dto.SemesterId.HasValue && dto.SemesterId.Value > 0)
            {
                var sem = await _dbContext.Semesters.FindAsync(dto.SemesterId.Value);
                if (sem != null && !sem.IsDeleted)
                {
                    currentDate = sem.StartDate;
                    endDate = sem.EndDate;
                    entity.StartDate = sem.StartDate;
                    entity.EndDate = sem.EndDate;
                    dto.StartDate = sem.StartDate;
                    dto.EndDate = sem.EndDate;
                }
            }

            if (endDate.HasValue)
            {
                // Dynamic ExpectedLessons based on Semester date range
                while (currentDate <= endDate.Value)
                {
                    var match = weeklySchedules.FirstOrDefault(w => (int)currentDate.DayOfWeek == w.DayOfWeek);
                    if (match != null)
                    {
                        var startSpan = TimeSpan.Parse(match.StartTime);
                        var endSpan = TimeSpan.Parse(match.EndTime);

                        var timeSlot = await _timeSlotRepository.FindAll()
                            .FirstOrDefaultAsync(ts => ts.StartTime == startSpan && ts.EndTime == endSpan);
                        if (timeSlot == null)
                        {
                            timeSlot = new TimeSlot
                            {
                                Code = $"TS_{match.StartTime.Replace(":", "")}_{match.EndTime.Replace(":", "")}",
                                Name = $"{match.StartTime} - {match.EndTime}",
                                StartTime = startSpan,
                                EndTime = endSpan
                            };
                            await _timeSlotRepository.AddAsync(timeSlot);
                            await _timeSlotRepository.SaveChangesAsync();
                        }

                        entity.ClassSchedules.Add(new ClassSchedule
                        {
                            LessonNo = lessonNo,
                            ScheduleDate = currentDate,
                            SlotId = timeSlot.Id,
                            RoomId = match.RoomId,
                            TeacherId = dto.TeacherId,
                            Status = 0, // Scheduled
                            Code = $"SCH_{entity.Code}_{lessonNo}",
                            Name = $"Buổi học {lessonNo} - {entity.Name}"
                        });
                        lessonNo++;
                    }
                    currentDate = currentDate.AddDays(1);
                }
                entity.ExpectedLessons = lessonNo - 1;
            }
            else
            {
                // Old fallback if no semester is bound
                int maxLessons = dto.ExpectedLessons.GetValueOrDefault(30);
                while (lessonNo <= maxLessons)
                {
                    var match = weeklySchedules.FirstOrDefault(w => (int)currentDate.DayOfWeek == w.DayOfWeek);
                    if (match != null)
                    {
                        var startSpan = TimeSpan.Parse(match.StartTime);
                        var endSpan = TimeSpan.Parse(match.EndTime);

                        var timeSlot = await _timeSlotRepository.FindAll()
                            .FirstOrDefaultAsync(ts => ts.StartTime == startSpan && ts.EndTime == endSpan);
                        if (timeSlot == null)
                        {
                            timeSlot = new TimeSlot
                            {
                                Code = $"TS_{match.StartTime.Replace(":", "")}_{match.EndTime.Replace(":", "")}",
                                Name = $"{match.StartTime} - {match.EndTime}",
                                StartTime = startSpan,
                                EndTime = endSpan
                            };
                            await _timeSlotRepository.AddAsync(timeSlot);
                            await _timeSlotRepository.SaveChangesAsync();
                        }

                        entity.ClassSchedules.Add(new ClassSchedule
                        {
                            LessonNo = lessonNo,
                            ScheduleDate = currentDate,
                            SlotId = timeSlot.Id,
                            RoomId = match.RoomId,
                            TeacherId = dto.TeacherId,
                            Status = 0, // Scheduled
                            Code = $"SCH_{entity.Code}_{lessonNo}",
                            Name = $"Buổi học {lessonNo} - {entity.Name}"
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

        public async Task<ApiResponse<List<ClassScheduleDto>>> GetTeacherSchedulesAsync(string username)
        {
            try
            {
                var user = await _userManager.FindByNameAsync(username);
                if (user == null)
                {
                    return ApiResponse<List<ClassScheduleDto>>.Fail("ERR_USER_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                var teacher = await _teacherRepository.FindAll()
                    .FirstOrDefaultAsync(t => t.Email == user.Email || t.Email == username);
                if (teacher == null)
                {
                    return ApiResponse<List<ClassScheduleDto>>.Fail("ERR_TEACHER_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                var schedules = await _scheduleRepository.FindAll()
                    .Include(cs => cs.TimeSlot)
                    .Include(cs => cs.Room)
                    .Include(cs => cs.Class)
                    .Where(cs => cs.TeacherId == teacher.Id && cs.Class != null && !cs.Class.IsDeleted)
                    .OrderBy(cs => cs.ScheduleDate)
                    .Select(cs => new ClassScheduleDto
                    {
                        Id = cs.Id,
                        ClassId = cs.ClassId,
                        ClassCode = cs.Class != null ? cs.Class.Code : null,
                        ClassName = cs.Class != null ? cs.Class.Name : null,
                        LessonNo = cs.LessonNo,
                        ScheduleDate = cs.ScheduleDate,
                        SlotId = cs.SlotId,
                        SlotName = cs.TimeSlot != null ? cs.TimeSlot.Name : null,
                        StartTime = cs.TimeSlot != null ? cs.TimeSlot.StartTime.ToString(@"hh\:mm") : null,
                        EndTime = cs.TimeSlot != null ? cs.TimeSlot.EndTime.ToString(@"hh\:mm") : null,
                        RoomId = cs.RoomId,
                        RoomName = cs.Room != null ? cs.Room.Name : null,
                        TeacherId = cs.TeacherId,
                        TeacherName = teacher.Name,
                        TeacherAvatar = teacher.Avatar,
                        Status = cs.Status,
                        Note = cs.Note
                    })
                    .ToListAsync();

                return ApiResponse<List<ClassScheduleDto>>.Ok(schedules, "GET_TEACHER_SCHEDULES_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<List<ClassScheduleDto>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<List<ClassScheduleDto>>> GetStudentSchedulesAsync(string username)
        {
            try
            {
                var user = await _userManager.FindByNameAsync(username);
                if (user == null)
                {
                    return ApiResponse<List<ClassScheduleDto>>.Fail("ERR_USER_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                var student = await _studentRepository.FindAll()
                    .Include(s => s.StudentClasses)
                    .FirstOrDefaultAsync(s => s.Email == user.Email || s.Email == username);
                if (student == null)
                {
                    return ApiResponse<List<ClassScheduleDto>>.Fail("ERR_STUDENT_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                var classIds = student.StudentClasses
                    .Where(sc => sc.Status == (int)StudentClassStatus.Enrolled || sc.Status == (int)StudentClassStatus.Studying)
                    .Select(sc => sc.ClassId)
                    .ToList();

                var schedules = await _scheduleRepository.FindAll()
                    .Include(cs => cs.TimeSlot)
                    .Include(cs => cs.Room)
                    .Include(cs => cs.Class)
                    .Include(cs => cs.Teacher)
                    .Where(cs => cs.ClassId.HasValue && classIds.Contains(cs.ClassId.Value) && cs.Class != null && !cs.Class.IsDeleted)
                    .OrderBy(cs => cs.ScheduleDate)
                    .Select(cs => new ClassScheduleDto
                    {
                        Id = cs.Id,
                        ClassId = cs.ClassId,
                        ClassCode = cs.Class != null ? cs.Class.Code : null,
                        ClassName = cs.Class != null ? cs.Class.Name : null,
                        LessonNo = cs.LessonNo,
                        ScheduleDate = cs.ScheduleDate,
                        SlotId = cs.SlotId,
                        SlotName = cs.TimeSlot != null ? cs.TimeSlot.Name : null,
                        StartTime = cs.TimeSlot != null ? cs.TimeSlot.StartTime.ToString(@"hh\:mm") : null,
                        EndTime = cs.TimeSlot != null ? cs.TimeSlot.EndTime.ToString(@"hh\:mm") : null,
                        RoomId = cs.RoomId,
                        RoomName = cs.Room != null ? cs.Room.Name : null,
                        TeacherId = cs.TeacherId,
                        TeacherName = cs.Teacher != null ? cs.Teacher.Name : (cs.Class != null && cs.Class.Teacher != null ? cs.Class.Teacher.Name : null),
                        TeacherAvatar = cs.Teacher != null ? cs.Teacher.Avatar : (cs.Class != null && cs.Class.Teacher != null ? cs.Class.Teacher.Avatar : null),
                        Status = cs.Status,
                        Note = cs.Note
                    })
                    .ToListAsync();

                return ApiResponse<List<ClassScheduleDto>>.Ok(schedules, "GET_STUDENT_SCHEDULES_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<List<ClassScheduleDto>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<List<ClassScheduleDto>>> GetChildSchedulesAsync(string username, int studentId)
        {
            try
            {
                var user = await _userManager.FindByNameAsync(username);
                if (user == null)
                {
                    return ApiResponse<List<ClassScheduleDto>>.Fail("ERR_USER_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                // Check permissions: parent must be mapped to this studentId, or admin/teacher
                var roles = await _userManager.GetRolesAsync(user);
                var isAdminOrTeacher = roles.Contains("Admin") || roles.Contains("Teacher");
                if (!isAdminOrTeacher)
                {
                    var isParentOfStudent = await _dbContext.ParentStudentLinks.AnyAsync(l => l.Parent.Email == user.Email && l.StudentId == studentId);
                    if (!isParentOfStudent)
                    {
                        return ApiResponse<List<ClassScheduleDto>>.Fail("ERR_UNAUTHORIZED", StatusCodes.Status403Forbidden);
                    }
                }

                var student = await _studentRepository.FindAll()
                    .Include(s => s.StudentClasses)
                    .FirstOrDefaultAsync(s => s.Id == studentId);
                if (student == null)
                {
                    return ApiResponse<List<ClassScheduleDto>>.Fail("ERR_STUDENT_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                var classIds = student.StudentClasses
                    .Where(sc => sc.Status == (int)StudentClassStatus.Enrolled || sc.Status == (int)StudentClassStatus.Studying)
                    .Select(sc => sc.ClassId)
                    .ToList();

                var schedules = await _scheduleRepository.FindAll()
                    .Include(cs => cs.TimeSlot)
                    .Include(cs => cs.Room)
                    .Include(cs => cs.Class)
                    .Include(cs => cs.Teacher)
                    .Where(cs => cs.ClassId.HasValue && classIds.Contains(cs.ClassId.Value) && cs.Class != null && !cs.Class.IsDeleted)
                    .OrderBy(cs => cs.ScheduleDate)
                    .Select(cs => new ClassScheduleDto
                    {
                        Id = cs.Id,
                        ClassId = cs.ClassId,
                        ClassCode = cs.Class != null ? cs.Class.Code : null,
                        ClassName = cs.Class != null ? cs.Class.Name : null,
                        LessonNo = cs.LessonNo,
                        ScheduleDate = cs.ScheduleDate,
                        SlotId = cs.SlotId,
                        SlotName = cs.TimeSlot != null ? cs.TimeSlot.Name : null,
                        StartTime = cs.TimeSlot != null ? cs.TimeSlot.StartTime.ToString(@"hh\:mm") : null,
                        EndTime = cs.TimeSlot != null ? cs.TimeSlot.EndTime.ToString(@"hh\:mm") : null,
                        RoomId = cs.RoomId,
                        RoomName = cs.Room != null ? cs.Room.Name : null,
                        TeacherId = cs.TeacherId,
                        TeacherName = cs.Teacher != null ? cs.Teacher.Name : (cs.Class != null && cs.Class.Teacher != null ? cs.Class.Teacher.Name : null),
                        TeacherAvatar = cs.Teacher != null ? cs.Teacher.Avatar : (cs.Class != null && cs.Class.Teacher != null ? cs.Class.Teacher.Avatar : null),
                        Status = cs.Status,
                        Note = cs.Note
                    })
                    .ToListAsync();

                return ApiResponse<List<ClassScheduleDto>>.Ok(schedules, "GET_CHILD_SCHEDULES_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<List<ClassScheduleDto>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<List<ClassScheduleDto>>> GetClassSchedulesAsync()
        {
            try
            {
                var schedules = await _scheduleRepository.FindAll()
                    .Include(cs => cs.TimeSlot)
                    .Include(cs => cs.Room)
                    .Include(cs => cs.Class)
                    .Include(cs => cs.Teacher)
                    .Where(cs => cs.Class != null && !cs.Class.IsDeleted)
                    .OrderBy(cs => cs.ScheduleDate)
                    .Select(cs => new ClassScheduleDto
                    {
                        Id = cs.Id,
                        ClassId = cs.ClassId,
                        ClassCode = cs.Class != null ? cs.Class.Code : null,
                        ClassName = cs.Class != null ? cs.Class.Name : null,
                        LessonNo = cs.LessonNo,
                        ScheduleDate = cs.ScheduleDate,
                        SlotId = cs.SlotId,
                        SlotName = cs.TimeSlot != null ? cs.TimeSlot.Name : null,
                        StartTime = cs.TimeSlot != null ? cs.TimeSlot.StartTime.ToString(@"hh\:mm") : null,
                        EndTime = cs.TimeSlot != null ? cs.TimeSlot.EndTime.ToString(@"hh\:mm") : null,
                        RoomId = cs.RoomId,
                        RoomName = cs.Room != null ? cs.Room.Name : null,
                        TeacherId = cs.TeacherId,
                        TeacherName = cs.Teacher != null ? cs.Teacher.Name : (cs.Class != null && cs.Class.Teacher != null ? cs.Class.Teacher.Name : null),
                        TeacherAvatar = cs.Teacher != null ? cs.Teacher.Avatar : (cs.Class != null && cs.Class.Teacher != null ? cs.Class.Teacher.Avatar : null),
                        Status = cs.Status,
                        Note = cs.Note
                    })
                    .ToListAsync();

                return ApiResponse<List<ClassScheduleDto>>.Ok(schedules, "GET_CLASS_SCHEDULES_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<List<ClassScheduleDto>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        private async Task ProcessNewStudentsAsync(ClassSaveDto dto)
        {
            if (dto.NewStudents == null || !dto.NewStudents.Any())
            {
                return;
            }

            var generatedCodes = new HashSet<string>();

            foreach (var newStudentDto in dto.NewStudents)
            {
                if (string.IsNullOrWhiteSpace(newStudentDto.Email))
                {
                    continue;
                }

                var existingStudent = await _studentRepository.FindAll()
                    .FirstOrDefaultAsync(s => s.Email != null && s.Email.ToLower() == newStudentDto.Email.Trim().ToLower());
                
                int studentId;
                if (existingStudent == null)
                {
                    string studentCode;
                    do
                    {
                        studentCode = await GenerateStudentCodeAsync();
                    } while (generatedCodes.Contains(studentCode));

                    generatedCodes.Add(studentCode);

                    var newStudent = new Student
                    {
                        Code = studentCode,
                        Name = newStudentDto.Name.Trim(),
                        Email = newStudentDto.Email.Trim(),
                        Phone = newStudentDto.Phone?.Trim(),
                        Status = 1
                    };
                    
                    await _studentRepository.AddAsync(newStudent);
                    await _studentRepository.SaveChangesAsync();
                    studentId = newStudent.Id;
                }
                else
                {
                    studentId = existingStudent.Id;
                }
                
                if (dto.StudentIds == null)
                {
                    dto.StudentIds = new List<int>();
                }
                
                if (!dto.StudentIds.Contains(studentId))
                {
                    dto.StudentIds.Add(studentId);
                }
            }
        }

        private async Task<string> GenerateStudentCodeAsync()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            string code;
            bool exists;
            do
            {
                var randomString = new string(Enumerable.Repeat(chars, 8)
                    .Select(s => s[random.Next(s.Length)]).ToArray());
                code = $"HS{randomString}";

                exists = await _dbContext.Students.IgnoreQueryFilters().AnyAsync(s => s.Code == code) ||
                         await _userManager.Users.AnyAsync(u => u.UserName == code);
            } while (exists);

            return code;
        }

        private async Task ProcessNewTeacherAsync(ClassSaveDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.NewTeacherEmail) || string.IsNullOrWhiteSpace(dto.NewTeacherName))
            {
                return;
            }

            var existingTeacher = await _teacherRepository.FindAll()
                .FirstOrDefaultAsync(t => t.Email != null && t.Email.ToLower() == dto.NewTeacherEmail.Trim().ToLower());

            int teacherId;
            if (existingTeacher == null)
            {
                var teacherCode = await GenerateTeacherCodeAsync();

                var newTeacher = new Teacher
                {
                    Code = teacherCode,
                    Name = dto.NewTeacherName.Trim(),
                    Email = dto.NewTeacherEmail.Trim(),
                    Status = 1
                };

                await _teacherRepository.AddAsync(newTeacher);
                await _teacherRepository.SaveChangesAsync();
                teacherId = newTeacher.Id;
            }
            else
            {
                teacherId = existingTeacher.Id;
            }

            dto.TeacherId = teacherId;
        }

        private async Task<string> GenerateTeacherCodeAsync()
        {
            var maxTeacher = await _teacherRepository.FindAll()
                .Where(t => t.Code != null && t.Code.StartsWith("GV"))
                .OrderByDescending(t => t.Code)
                .FirstOrDefaultAsync();

            if (maxTeacher != null && maxTeacher.Code.Length > 2)
            {
                var numStr = maxTeacher.Code.Substring(2);
                if (int.TryParse(numStr, out int num))
                {
                    return $"GV{(num + 1):D5}";
                }
            }
            return "GV00001";
        }

        private async Task ProcessNewCourseAsync(ClassSaveDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.NewCourseName))
            {
                return;
            }

            var existingCourse = await _courseRepository.FindAll()
                .FirstOrDefaultAsync(c => c.Name != null && c.Name.ToLower() == dto.NewCourseName.Trim().ToLower());

            int courseId;
            if (existingCourse == null)
            {
                var courseCode = await GenerateCourseCodeAsync();

                var newCourse = new Course
                {
                    Code = courseCode,
                    Name = dto.NewCourseName.Trim(),
                    Status = 1
                };

                await _courseRepository.AddAsync(newCourse);
                await _courseRepository.SaveChangesAsync();
                courseId = newCourse.Id;
            }
            else
            {
                courseId = existingCourse.Id;
            }

            dto.CourseId = courseId;
        }

        private async Task<string> GenerateCourseCodeAsync()
        {
            var maxCourse = await _courseRepository.FindAll()
                .Where(c => c.Code != null && c.Code.StartsWith("KH"))
                .OrderByDescending(c => c.Code)
                .FirstOrDefaultAsync();

            if (maxCourse != null && maxCourse.Code.Length > 2)
            {
                var numStr = maxCourse.Code.Substring(2);
                if (int.TryParse(numStr, out int num))
                {
                    return $"KH{(num + 1):D5}";
                }
            }
            return "KH00001";
        }

        private async Task AutoUpdateClassStatusesAsync()
        {
            try
            {
                var today = DateTime.Today;

                var planningClasses = await _repository.FindAll(trackChanges: true)
                    .Where(c => c.Status == (int)ClassStatus.Planning 
                             && c.StartDate.HasValue 
                             && today >= c.StartDate.Value.Date)
                    .ToListAsync();

                foreach (var c in planningClasses)
                {
                    c.Status = (int)ClassStatus.Active;
                }

                var activeClasses = await _repository.FindAll(trackChanges: true)
                    .Where(c => c.Status == (int)ClassStatus.Active 
                             && c.EndDate.HasValue 
                             && today > c.EndDate.Value.Date)
                    .ToListAsync();

                foreach (var c in activeClasses)
                {
                    c.Status = (int)ClassStatus.Completed;
                }

                if (planningClasses.Any() || activeClasses.Any())
                {
                    await _repository.SaveChangesAsync();
                }
            }
            catch (Exception)
            {
                // Suppress exception
            }
        }
    }
}

