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
using sep490_be.DTO.Common;

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
        private readonly IBaseRepository<StudentRegistration, ApplicationDbContext> _studentRegistrationRepository;
        private readonly IBaseRepository<StudentClass, ApplicationDbContext> _studentClassRepository;
        private readonly IBaseRepository<Room, ApplicationDbContext> _roomRepository;
        private readonly IBaseRepository<Semester, ApplicationDbContext> _semesterRepository;
        private readonly IBaseRepository<Attendance, ApplicationDbContext> _attendanceRepository;
        private readonly IBaseRepository<ParentStudentLink, ApplicationDbContext> _parentStudentLinkRepository;
        private readonly IScheduleOptimizationService _optService;
        private readonly INotificationService _notificationService;
 
        public ClassService(
            IClassRepository repository,
            ICourseRepository courseRepository,
            ITeacherRepository teacherRepository,
            IStudentRepository studentRepository,
            IBaseRepository<TimeSlot, ApplicationDbContext> timeSlotRepository,
            IBaseRepository<ClassSchedule, ApplicationDbContext> scheduleRepository,
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IBaseRepository<StudentRegistration, ApplicationDbContext> studentRegistrationRepository,
            IBaseRepository<StudentClass, ApplicationDbContext> studentClassRepository,
            IBaseRepository<Room, ApplicationDbContext> roomRepository,
            IBaseRepository<Semester, ApplicationDbContext> semesterRepository,
            IBaseRepository<Attendance, ApplicationDbContext> attendanceRepository,
            IBaseRepository<ParentStudentLink, ApplicationDbContext> parentStudentLinkRepository,
            IScheduleOptimizationService optService,
            INotificationService notificationService)
        {
            _repository = repository;
            _courseRepository = courseRepository;
            _teacherRepository = teacherRepository;
            _studentRepository = studentRepository;
            _timeSlotRepository = timeSlotRepository;
            _scheduleRepository = scheduleRepository;
            _userManager = userManager;
            _roleManager = roleManager;
            _studentRegistrationRepository = studentRegistrationRepository;
            _studentClassRepository = studentClassRepository;
            _roomRepository = roomRepository;
            _semesterRepository = semesterRepository;
            _attendanceRepository = attendanceRepository;
            _parentStudentLinkRepository = parentStudentLinkRepository;
            _optService = optService;
            _notificationService = notificationService;
        }

        public async Task<ApiResponse<PagingResponse<ClassDto>>> GetAllAsync(ClassSearchDto searchDto)
        {
            try
            {
                await AutoUpdateClassStatusesAsync();
                var query = _repository.GetClassesWithBasicDetails();

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

                if (searchDto.Type.HasValue)
                {
                    query = query.Where(c => c.Type == searchDto.Type.Value);
                }

                query = query.OrderByDescending(c => c.Id);
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
                var entity = await _repository.GetClassWithDetailsByIdAsync(id);

                if (entity == null)
                {
                    return ApiResponse<ClassDto>.Fail("ERR_CLASS_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                if (!string.IsNullOrEmpty(username))
                {
                    var user = await _userManager.FindByNameAsync(username);
                    if (user != null)
                    {
                        var userClaims = await _userManager.GetClaimsAsync(user);
                        var permissions = userClaims
                            .Where(c => c.Type.Equals("Permission", StringComparison.OrdinalIgnoreCase))
                            .Select(c => c.Value)
                            .ToList();

                        var roles = await _userManager.GetRolesAsync(user);
                        foreach (var roleName in roles)
                        {
                            var role = await _roleManager.FindByNameAsync(roleName);
                            if (role != null)
                            {
                                var roleClaims = await _roleManager.GetClaimsAsync(role);
                                foreach (var claim in roleClaims)
                                {
                                    if (claim.Type.Equals("Permission", StringComparison.OrdinalIgnoreCase))
                                    {
                                        permissions.Add(claim.Value);
                                    }
                                }
                            }
                        }

                        bool isViewAll = permissions.Contains(Permissions.Class.Class_View) || 
                                         permissions.Contains(Permissions.Class.ClassPage);

                        if (!isViewAll)
                        {
                            if (permissions.Contains(Permissions.TeachingClass.TeachingClassPage))
                            {
                                var teacher = await _teacherRepository.FindAll()
                                    .FirstOrDefaultAsync(t => t.Email == user.Email || t.Email == username);
                                if (teacher == null || entity.TeacherId != teacher.Id)
                                {
                                    return ApiResponse<ClassDto>.Fail("ERR_FORBIDDEN", StatusCodes.Status403Forbidden);
                                }
                            }
                            else if (permissions.Contains(Permissions.MyClass.MyClassPage))
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
            using var transaction = await _repository.BeginTransactionAsync();
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

                if (dto.Students != null && dto.Students.Any())
                {
                    foreach (var s in dto.Students)
                    {
                        entity.StudentClasses.Add(new StudentClass
                        {
                            StudentId = s.StudentId,
                            EnrollDate = DateTime.UtcNow,
                            Status = (int)StudentClassStatus.Enrolled,
                            EnrollType = s.EnrollType
                        });
                    }
                }

                await GenerateSchedulesAsync(entity, dto);

                // Update student registrations to Scheduled
                if (dto.Students != null && dto.Students.Any() && entity.CourseId.HasValue)
                {
                    var addedStudentIds = dto.Students.Select(s => s.StudentId).ToList();
                    var semesterId = entity.SemesterId;
                    var regsToUpdate = await _studentRegistrationRepository.FindAll(trackChanges: true)
                        .Where(r => r.CourseId == entity.CourseId
                                 && r.Status == (int)StudentRegistrationStatus.Pending
                                 && addedStudentIds.Contains(r.StudentId)
                                 && (!semesterId.HasValue || r.SemesterId == semesterId.Value))
                        .ToListAsync();

                    foreach (var reg in regsToUpdate)
                    {
                        reg.Status = (int)StudentRegistrationStatus.Scheduled;
                        await _studentRegistrationRepository.UpdateAsync(reg);
                    }
                }

                await _repository.AddAsync(entity);
                await _repository.SaveChangesAsync();

                await transaction.CommitAsync();

                // Reload to populate relationships for return value
                var createdClass = await _repository.GetClassWithBasicDetailsByIdAsync(entity.Id);

                // Trigger SignalR Notification
                if (createdClass != null)
                {
                    await _notificationService.SendClassCreatedNotificationAsync(createdClass);
                }

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
            using var transaction = await _repository.BeginTransactionAsync();
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

                var existingEntity = await _repository.GetClassForEditAsync(dto.Id);

                if (existingEntity == null)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<ClassDto>.Fail("ERR_CLASS_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                int oldStatus = existingEntity.Status;
                int? oldTeacherId = existingEntity.TeacherId;

                // Delete old schedules first to avoid orphans
                if (existingEntity.ClassSchedules != null && existingEntity.ClassSchedules.Any())
                {
                    await _scheduleRepository.DeleteRangeAsync(existingEntity.ClassSchedules.ToList());
                }

                // Sync StudentClasses
                var currentStudentIds = existingEntity.StudentClasses.Select(sc => sc.StudentId).ToList();
                var newStudents = dto.Students ?? new List<StudentEnrollDto>();
                var newStudentIds = newStudents.Select(s => s.StudentId).ToList();

                // Remove students that are no longer assigned
                var studentsToRemove = existingEntity.StudentClasses.Where(sc => !newStudentIds.Contains(sc.StudentId)).ToList();
                if (studentsToRemove.Any())
                {
                    await _studentClassRepository.DeleteRangeAsync(studentsToRemove);
                }

                // Add new students or update EnrollType for existing ones
                var studentsToAdd = newStudents.Where(s => !currentStudentIds.Contains(s.StudentId)).ToList();
                foreach (var s in studentsToAdd)
                {
                    existingEntity.StudentClasses.Add(new StudentClass
                    {
                        StudentId = s.StudentId,
                        EnrollDate = DateTime.UtcNow,
                        Status = (int)StudentClassStatus.Enrolled,
                        EnrollType = s.EnrollType
                    });
                }

                // Update EnrollType for already-existing students
                foreach (var existingSc in existingEntity.StudentClasses.Where(sc => newStudentIds.Contains(sc.StudentId)))
                {
                    var updated = newStudents.FirstOrDefault(s => s.StudentId == existingSc.StudentId);
                    if (updated != null)
                        existingSc.EnrollType = updated.EnrollType;
                }

                // Sync student registrations status
                if (existingEntity.CourseId.HasValue)
                {
                    var semesterId = existingEntity.SemesterId;
                    
                    // Reset removed students' registrations to Pending
                    var removeStudentIds = studentsToRemove.Select(sc => sc.StudentId).ToList();
                    if (removeStudentIds.Any())
                    {
                        var regsToReset = await _studentRegistrationRepository.FindAll(trackChanges: true)
                            .Where(r => r.CourseId == existingEntity.CourseId
                                     && r.Status == (int)StudentRegistrationStatus.Scheduled
                                     && removeStudentIds.Contains(r.StudentId)
                                     && (!semesterId.HasValue || r.SemesterId == semesterId.Value))
                            .ToListAsync();

                        foreach (var reg in regsToReset)
                        {
                            reg.Status = (int)StudentRegistrationStatus.Pending;
                            await _studentRegistrationRepository.UpdateAsync(reg);
                        }
                    }

                    // Update added students' registrations to Scheduled
                    var addStudentIds = studentsToAdd.Select(s => s.StudentId).ToList();
                    if (addStudentIds.Any())
                    {
                        var regsToUpdate = await _studentRegistrationRepository.FindAll(trackChanges: true)
                            .Where(r => r.CourseId == existingEntity.CourseId
                                     && r.Status == (int)StudentRegistrationStatus.Pending
                                     && addStudentIds.Contains(r.StudentId)
                                     && (!semesterId.HasValue || r.SemesterId == semesterId.Value))
                            .ToListAsync();

                        foreach (var reg in regsToUpdate)
                        {
                            reg.Status = (int)StudentRegistrationStatus.Scheduled;
                            await _studentRegistrationRepository.UpdateAsync(reg);
                        }
                    }
                }

                // Map basic fields
                dto.Adapt(existingEntity);

                // Re-generate schedules
                await GenerateSchedulesAsync(existingEntity, dto);

                await _repository.SaveChangesAsync();

                await transaction.CommitAsync();

                // Reload to populate relationships for return value
                var updatedClass = await _repository.GetClassWithBasicDetailsByIdAsync(existingEntity.Id);

                // Trigger SignalR Notification if class status changed
                if (updatedClass != null && oldStatus != updatedClass.Status)
                {
                    await _notificationService.SendClassStatusChangedNotificationAsync(updatedClass, oldStatus, updatedClass.Status);
                }

                // Trigger SignalR Notification for newly added students
                if (updatedClass != null && studentsToAdd.Any())
                {
                    await _notificationService.SendStudentsAddedToClassNotificationAsync(updatedClass, studentsToAdd.Select(s => s.StudentId).ToList());
                }

                // Trigger SignalR Notification if teacher changed or newly assigned
                if (updatedClass != null && updatedClass.TeacherId.HasValue
                    && updatedClass.TeacherId != oldTeacherId)
                {
                    await _notificationService.SendTeacherAssignedToClassNotificationAsync(updatedClass, updatedClass.TeacherId.Value);
                }

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
            using var transaction = await _repository.BeginTransactionAsync();
            try
            {
                var existingEntity = await _repository.GetByIdAsync(id);
                if (existingEntity == null)
                {
                    return ApiResponse<bool>.Fail("ERR_CLASS_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                if (existingEntity.Status != (int)ClassStatus.Planning)
                {
                    return ApiResponse<bool>.Fail("ERR_CLASS_ALREADY_STARTED", StatusCodes.Status400BadRequest);
                }

                // 1. Get student IDs enrolled in this class
                var studentClasses = await _studentClassRepository.FindAll()
                    .Where(sc => sc.ClassId == id)
                    .ToListAsync();
                var studentIds = studentClasses.Select(sc => sc.StudentId).ToList();

                if (studentIds.Any() && existingEntity.CourseId.HasValue)
                {
                    var semesterId = existingEntity.SemesterId;
                    var regsToReset = await _studentRegistrationRepository.FindAll(trackChanges: true)
                        .Where(r => r.CourseId == existingEntity.CourseId 
                                 && r.Status == (int)StudentRegistrationStatus.Scheduled 
                                 && studentIds.Contains(r.StudentId)
                                 && (!semesterId.HasValue || r.SemesterId == semesterId.Value))
                        .ToListAsync();

                    foreach (var reg in regsToReset)
                    {
                        reg.Status = (int)StudentRegistrationStatus.Pending;
                        await _studentRegistrationRepository.UpdateAsync(reg);
                    }
                }

                 // Remove StudentClasses relations to avoid orphans
                 if (studentClasses.Any())
                 {
                     await _studentClassRepository.DeleteRangeAsync(studentClasses);
                 }

                // Delete the class itself
                await _repository.DeleteAsync(existingEntity);
                await _repository.SaveChangesAsync();
                
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

                query = query.OrderByDescending(c => c.Id);
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

                query = query.OrderByDescending(c => c.Id);
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

        public async Task<ApiResponse<PagingResponse<ClassDto>>> GetAccessibleClassesAsync(string username, ClassSearchDto searchDto)
        {
            try
            {
                var user = await _userManager.FindByNameAsync(username);
                var email = user?.Email ?? username;

                // 1. Check if Teacher
                var teacher = await _teacherRepository.FindAll()
                    .FirstOrDefaultAsync(t => t.Email == email || t.Code == username);
                if (teacher != null)
                {
                    return await GetTeacherClassesAsync(username, searchDto);
                }

                // 2. Check if Student
                var student = await _studentRepository.FindAll()
                    .FirstOrDefaultAsync(s => s.Email == email || s.Code == username);
                if (student != null)
                {
                    return await GetStudentClassesAsync(username, searchDto);
                }

                // 3. Fallback to Admin (All)
                return await GetAllAsync(searchDto);
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
            Type = entity.Type,
            TypeName = entity.Type == 1 ? "Online" : "Offline",
            Url = entity.Url,
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
                EnrollType = sc.EnrollType,
                EnrollTypeName = sc.EnrollType == 1 ? "Online" : "Offline",
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

            // Kiểm tra sức chứa phòng học so với số lượng học sinh (chỉ áp dụng cho lớp Offline)
            int studentCount = dto.Students?.Count ?? 0;
            if (dto.Type != 1 && dto.WeeklySchedules != null && dto.WeeklySchedules.Any())
            {
                var roomIds = dto.WeeklySchedules
                    .Where(w => w.RoomId.HasValue)
                    .Select(w => w.RoomId.Value)
                    .Distinct()
                    .ToList();

                if (roomIds.Any())
                {
                    var rooms = await _roomRepository.FindAll()
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

            // 1. Kiểm tra trạng thái đăng ký của học sinh trong học kỳ - khóa học (không được thêm học sinh đã xếp lớp ở lớp khác)
            if (dto.SemesterId.HasValue && dto.SemesterId.Value > 0 && dto.CourseId.HasValue && dto.StudentIds != null && dto.StudentIds.Any())
            {
                var alreadyScheduledStudents = await _studentRegistrationRepository.FindAll()
                    .Include(r => r.Student)
                    .Where(r => r.SemesterId == dto.SemesterId.Value
                             && r.CourseId == dto.CourseId.Value
                             && r.Status == (int)StudentRegistrationStatus.Scheduled
                             && dto.StudentIds.Contains(r.StudentId))
                    .ToListAsync();

                if (alreadyScheduledStudents.Any())
                {
                    var trulyConflicting = alreadyScheduledStudents;
                    if (isEdit)
                    {
                        var alreadyInThisClassIds = await _studentClassRepository.FindAll()
                            .Where(sc => sc.ClassId == dto.Id)
                            .Select(sc => sc.StudentId)
                            .ToListAsync();

                        trulyConflicting = alreadyScheduledStudents
                            .Where(s => !alreadyInThisClassIds.Contains(s.StudentId))
                            .ToList();
                    }

                    if (trulyConflicting.Any())
                    {
                        var conflictEmails = trulyConflicting
                            .Select(r => r.Student?.Email ?? r.StudentId.ToString())
                            .Distinct()
                            .ToList();
                        return $"ERR_STUDENT_CONFLICT_{conflictEmails.Count}__{string.Join(",", conflictEmails)}";
                    }
                }
            }

            // 2. Kiểm tra trùng lịch học của học sinh (1 học sinh chỉ được học 1 lớp tại 1 thời điểm)
            DateTime? proposedStartDate = dto.StartDate;
            DateTime? proposedEndDate = null;
            int? expectedLessons = dto.ExpectedLessons;

            if (dto.SemesterId.HasValue && dto.SemesterId.Value > 0)
            {
                var sem = await _semesterRepository.GetByIdAsync(dto.SemesterId.Value);
                if (sem != null && !sem.IsDeleted)
                {
                    proposedStartDate = sem.StartDate;
                    proposedEndDate = sem.EndDate;
                }
            }
            else if (dto.StartDate.HasValue && expectedLessons.HasValue && expectedLessons.Value > 0 && dto.WeeklySchedules != null && dto.WeeklySchedules.Any())
            {
                var currentDate = dto.StartDate.Value;
                int lessonNo = 1;
                var weeklySchedules = dto.WeeklySchedules.OrderBy(w => w.DayOfWeek).ToList();
                if (!weeklySchedules.Any(w => w.DayOfWeek < 0 || w.DayOfWeek > 6))
                {
                    while (lessonNo <= expectedLessons.Value)
                    {
                        var match = weeklySchedules.FirstOrDefault(w => (int)currentDate.DayOfWeek == w.DayOfWeek);
                        if (match != null)
                        {
                            if (lessonNo == expectedLessons.Value)
                            {
                                proposedEndDate = currentDate;
                            }
                            lessonNo++;
                        }
                        currentDate = currentDate.AddDays(1);
                    }
                }
            }

            if (dto.StudentIds != null && dto.StudentIds.Any() && proposedStartDate.HasValue && proposedEndDate.HasValue && dto.WeeklySchedules != null && dto.WeeklySchedules.Any())
            {
                var otherStudentClasses = await _studentClassRepository.FindAll()
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

                    var otherSchedules = await _scheduleRepository.FindAll()
                        .Include(cs => cs.TimeSlot)
                        .Where(cs => cs.ClassId.HasValue
                                  && otherClassIds.Contains(cs.ClassId.Value)
                                  && cs.Class != null
                                  && !cs.Class.IsDeleted
                                  && cs.ScheduleDate >= proposedStartDate
                                  && cs.ScheduleDate <= proposedEndDate)
                        .ToListAsync();

                    if (otherSchedules.Any())
                    {
                        // Generate proposed schedules for comparison
                        var propSchedules = new List<(DateTime Date, TimeSpan Start, TimeSpan End)>();
                        var currentDate = proposedStartDate.Value;
                        var weeklySchedules = dto.WeeklySchedules.OrderBy(w => w.DayOfWeek).ToList();

                        if (dto.SemesterId.HasValue && dto.SemesterId.Value > 0)
                        {
                            while (currentDate <= proposedEndDate.Value)
                            {
                                var match = weeklySchedules.FirstOrDefault(w => (int)currentDate.DayOfWeek == w.DayOfWeek);
                                if (match != null && TimeSpan.TryParse(match.StartTime, out var startSpan) && TimeSpan.TryParse(match.EndTime, out var endSpan))
                                {
                                    propSchedules.Add((currentDate, startSpan, endSpan));
                                }
                                currentDate = currentDate.AddDays(1);
                            }
                        }
                        else
                        {
                            int lessonNo = 1;
                            while (lessonNo <= expectedLessons.GetValueOrDefault(30))
                            {
                                var match = weeklySchedules.FirstOrDefault(w => (int)currentDate.DayOfWeek == w.DayOfWeek);
                                if (match != null && TimeSpan.TryParse(match.StartTime, out var startSpan) && TimeSpan.TryParse(match.EndTime, out var endSpan))
                                {
                                    propSchedules.Add((currentDate, startSpan, endSpan));
                                    lessonNo++;
                                }
                                currentDate = currentDate.AddDays(1);
                            }
                        }

                        var conflictingStudentIds = new HashSet<int>();
                        foreach (var sc in otherStudentClasses)
                        {
                            var classSchedules = otherSchedules.Where(s => s.ClassId == sc.ClassId).ToList();
                            bool hasScheduleConflict = false;

                            foreach (var prop in propSchedules)
                            {
                                foreach (var ext in classSchedules)
                                {
                                    if (ext.ScheduleDate?.Date == prop.Date.Date && ext.TimeSlot != null)
                                    {
                                        bool timeOverlaps = ext.TimeSlot.StartTime < prop.End && ext.TimeSlot.EndTime > prop.Start;
                                        if (timeOverlaps)
                                        {
                                            hasScheduleConflict = true;
                                            break;
                                        }
                                    }
                                }
                                if (hasScheduleConflict) break;
                            }

                            if (hasScheduleConflict)
                            {
                                conflictingStudentIds.Add(sc.StudentId);
                            }
                        }

                        if (conflictingStudentIds.Any())
                        {
                            var conflictingEmails = otherStudentClasses
                                .Where(sc => conflictingStudentIds.Contains(sc.StudentId) && sc.Student != null && !string.IsNullOrWhiteSpace(sc.Student.Email))
                                .Select(sc => sc.Student!.Email!.Trim())
                                .Distinct()
                                .ToList();

                            if (conflictingEmails.Any())
                            {
                                return $"ERR_STUDENT_CONFLICT_{conflictingEmails.Count}__{string.Join(",", conflictingEmails)}";
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
                var sem = await _semesterRepository.GetByIdAsync(dto.SemesterId.Value);
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
                await AutoUpdateClassStatusesAsync();
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
                        Note = cs.Note,
                        ClassStatus = cs.Class != null ? cs.Class.Status : null
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
                await AutoUpdateClassStatusesAsync();
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
                        Note = cs.Note,
                        ClassStatus = cs.Class != null ? cs.Class.Status : null
                    })
                    .ToListAsync();

                if (schedules.Any())
                {
                    var scheduleIds = schedules.Select(s => s.Id).ToList();
                    var attendances = await _attendanceRepository.FindAll()
                        .Where(a => a.StudentId == student.Id && a.ScheduleId.HasValue && scheduleIds.Contains(a.ScheduleId.Value) && !a.IsDeleted)
                        .ToDictionaryAsync(a => a.ScheduleId!.Value, a => a.Status);

                    foreach (var s in schedules)
                    {
                        if (attendances.TryGetValue(s.Id, out var attStatus))
                        {
                            s.AttendanceStatus = attStatus;
                        }
                    }
                }

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
                await AutoUpdateClassStatusesAsync();
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
                    var isParentOfStudent = await _parentStudentLinkRepository.FindAll().AnyAsync(l => l.Parent.Email == user.Email && l.StudentId == studentId);
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
                        Note = cs.Note,
                        ClassStatus = cs.Class != null ? cs.Class.Status : null
                    })
                    .ToListAsync();

                if (schedules.Any())
                {
                    var scheduleIds = schedules.Select(s => s.Id).ToList();
                    var attendances = await _attendanceRepository.FindAll()
                        .Where(a => a.StudentId == student.Id && a.ScheduleId.HasValue && scheduleIds.Contains(a.ScheduleId.Value) && !a.IsDeleted)
                        .ToDictionaryAsync(a => a.ScheduleId!.Value, a => a.Status);

                    foreach (var s in schedules)
                    {
                        if (attendances.TryGetValue(s.Id, out var attStatus))
                        {
                            s.AttendanceStatus = attStatus;
                        }
                    }
                }

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
                await AutoUpdateClassStatusesAsync();
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
                        Note = cs.Note,
                        ClassStatus = cs.Class != null ? cs.Class.Status : null
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
                
                // Add newly created/resolved student to the Students list with default enroll type matching the class type
                if (!dto.Students.Any(s => s.StudentId == studentId))
                {
                    dto.Students.Add(new StudentEnrollDto
                    {
                        StudentId = studentId,
                        EnrollType = dto.Type // default enroll type matches class type
                    });
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

                exists = await _studentRepository.FindAll().IgnoreQueryFilters().AnyAsync(s => s.Code == code) ||
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

                var statusChanges = new List<(Class Class, int Old, int New)>();

                foreach (var c in planningClasses)
                {
                    statusChanges.Add((c, (int)ClassStatus.Planning, (int)ClassStatus.Active));
                    c.Status = (int)ClassStatus.Active;
                }

                var activeClasses = await _repository.FindAll(trackChanges: true)
                    .Where(c => c.Status == (int)ClassStatus.Active 
                             && c.EndDate.HasValue 
                             && today > c.EndDate.Value.Date)
                    .ToListAsync();

                foreach (var c in activeClasses)
                {
                    statusChanges.Add((c, (int)ClassStatus.Active, (int)ClassStatus.Completed));
                    c.Status = (int)ClassStatus.Completed;
                }

                if (planningClasses.Any() || activeClasses.Any())
                {
                    await _repository.SaveChangesAsync();

                    foreach (var change in statusChanges)
                    {
                        await _notificationService.SendClassStatusChangedNotificationAsync(change.Class, change.Old, change.New);
                    }
                }
            }
            catch (Exception)
            {
                // Suppress exception
            }
        }
    }
}

