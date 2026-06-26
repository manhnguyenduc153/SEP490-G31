using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Mapster;
using PRN232_be.DTO;
using PRN232_be.DTO.Class;
using PRN232_be.Models;
using PRN232_be.Repositories.Interfaces;
using PRN232_be.Services.Interfaces;
using PRN232_be.Helpers;
using PRN232_be.Enums;
using PRN232_be.Repositories.Common;
using Microsoft.AspNetCore.Identity;

namespace PRN232_be.Services.Implementations
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
 
        public ClassService(
            IClassRepository repository,
            ICourseRepository courseRepository,
            ITeacherRepository teacherRepository,
            IStudentRepository studentRepository,
            IBaseRepository<TimeSlot, ApplicationDbContext> timeSlotRepository,
            IBaseRepository<ClassSchedule, ApplicationDbContext> scheduleRepository,
            UserManager<IdentityUser> userManager)
        {
            _repository = repository;
            _courseRepository = courseRepository;
            _teacherRepository = teacherRepository;
            _studentRepository = studentRepository;
            _timeSlotRepository = timeSlotRepository;
            _scheduleRepository = scheduleRepository;
            _userManager = userManager;
        }

        public async Task<ApiResponse<PagingResponse<ClassDto>>> GetAllAsync(ClassSearchDto searchDto)
        {
            try
            {
                var query = _repository.FindAll()
                    .Include(c => c.Course)
                    .Include(c => c.Teacher)
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

        public async Task<ApiResponse<ClassDto>> GetByIdAsync(int id)
        {
            try
            {
                var entity = await _repository.FindAll()
                    .Include(c => c.Course)
                    .Include(c => c.Teacher)
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

                return ApiResponse<ClassDto>.Ok(MapToDto(entity), "GET_CLASS_DETAIL_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<ClassDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<ClassDto>> CreateAsync(ClassSaveDto dto)
        {
            try
            {
                var validationError = await ValidateAsync(dto, isEdit: false);
                if (validationError != null)
                {
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

                // Reload to populate relationships for return value
                var createdClass = await _repository.FindAll()
                    .Include(c => c.Course)
                    .Include(c => c.Teacher)
                    .Include(c => c.StudentClasses)
                    .FirstOrDefaultAsync(c => c.Id == entity.Id);

                return ApiResponse<ClassDto>.Created(MapToDto(createdClass ?? entity), "CREATE_CLASS_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<ClassDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<ClassDto>> EditAsync(ClassSaveDto dto)
        {
            try
            {
                var validationError = await ValidateAsync(dto, isEdit: true);
                if (validationError != null)
                {
                    return ApiResponse<ClassDto>.Fail(validationError, StatusCodes.Status400BadRequest);
                }

                var existingEntity = await _repository.FindAll(trackChanges: true)
                    .Include(c => c.StudentClasses)
                    .Include(c => c.ClassSchedules)
                    .FirstOrDefaultAsync(c => c.Id == dto.Id);

                if (existingEntity == null)
                {
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

                // Reload to populate relationships for return value
                var updatedClass = await _repository.FindAll()
                    .Include(c => c.Course)
                    .Include(c => c.Teacher)
                    .Include(c => c.StudentClasses)
                    .FirstOrDefaultAsync(c => c.Id == existingEntity.Id);

                return ApiResponse<ClassDto>.Ok(MapToDto(updatedClass ?? existingEntity), "UPDATE_CLASS_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<ClassDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<bool>> DeleteAsync(int id)
        {
            try
            {
                var existingEntity = await _repository.GetByIdAsync(id);
                if (existingEntity == null)
                {
                    return ApiResponse<bool>.Fail("ERR_CLASS_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                await _repository.DeleteAsync(existingEntity);
                await _repository.SaveChangesAsync();

                return ApiResponse<bool>.Ok(true, "DELETE_CLASS_SUCCESS");
            }
            catch (Exception ex)
            {
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
            }).OrderBy(cs => cs.LessonNo).ToList() ?? new List<ClassScheduleDto>()
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

            if (!dto.ExpectedLessons.HasValue || dto.ExpectedLessons.Value <= 0)
                return "ERR_EXPECTED_LESSONS_INVALID";

            if (dto.WeeklySchedules == null || !dto.WeeklySchedules.Any())
                return "ERR_WEEKLY_SCHEDULES_EMPTY";

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
            if (dto.WeeklySchedules == null || !dto.WeeklySchedules.Any() || !dto.StartDate.HasValue || !dto.ExpectedLessons.HasValue || dto.ExpectedLessons.Value <= 0)
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
            entity.ExpectedLessons = dto.ExpectedLessons;
            entity.AutoRefund = dto.AutoRefund;

            // Clear the existing navigation collection
            entity.ClassSchedules.Clear();

            // Generate dates
            var currentDate = dto.StartDate.Value;
            int lessonNo = 1;
            var weeklySchedules = dto.WeeklySchedules.OrderBy(w => w.DayOfWeek).ToList();

            // Guard against infinite loops if the DayOfWeek values are invalid
            if (weeklySchedules.Any(w => w.DayOfWeek < 0 || w.DayOfWeek > 6))
            {
                return;
            }

            while (lessonNo <= dto.ExpectedLessons.Value)
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

            // Calculate EndDate
            if (entity.ClassSchedules.Any())
            {
                entity.EndDate = entity.ClassSchedules.Last().ScheduleDate;
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
                    .Where(cs => cs.TeacherId == teacher.Id)
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
                    .Where(cs => cs.ClassId.HasValue && classIds.Contains(cs.ClassId.Value))
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
    }
}
