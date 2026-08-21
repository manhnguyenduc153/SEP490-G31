using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using sep490_be.DTO;
using sep490_be.DTO.Semester;
using sep490_be.DTO.Teacher;
using sep490_be.DTO.Student;
using sep490_be.Models;
using sep490_be.Services.Interfaces;
using sep490_be.Enums;
using sep490_be.Helpers;
using sep490_be.Repositories.Common;
using sep490_be.Repositories.Interfaces;

namespace sep490_be.Services.Implementations
{
    public class SemesterService : ISemesterService
    {
        private readonly IStudentRegistrationRepository _studentRegistrationRepository;
        private readonly ISemesterRepository _semesterRepository;
        private readonly IClassRepository _classRepository;
        private readonly IBaseRepository<ClassSchedule, ApplicationDbContext> _scheduleRepository;
        private readonly IBaseRepository<TeacherAvailability, ApplicationDbContext> _availabilityRepository;
        private readonly ICourseRepository _courseRepository;
        private readonly IStudentRepository _studentRepository;

        public SemesterService(
            IStudentRegistrationRepository studentRegistrationRepository,
            ISemesterRepository semesterRepository,
            IClassRepository classRepository,
            IBaseRepository<ClassSchedule, ApplicationDbContext> scheduleRepository,
            IBaseRepository<TeacherAvailability, ApplicationDbContext> availabilityRepository,
            ICourseRepository courseRepository,
            IStudentRepository studentRepository)
        {
            _studentRegistrationRepository = studentRegistrationRepository;
            _semesterRepository = semesterRepository;
            _classRepository = classRepository;
            _scheduleRepository = scheduleRepository;
            _availabilityRepository = availabilityRepository;
            _courseRepository = courseRepository;
            _studentRepository = studentRepository;
        }

        public async Task<ApiResponse<List<SemesterDto>>> GetAllAsync()
        {
            try
            {
                var entities = await _semesterRepository.FindAll()
                    .Where(s => !s.IsDeleted)
                    .OrderByDescending(s => s.Id)
                    .ToListAsync();

                var semesterIds = entities.Select(e => e.Id).ToList();
                var classCounts = await _classRepository.FindAll()
                    .Where(c => !c.IsDeleted && c.SemesterId != null && semesterIds.Contains(c.SemesterId.Value))
                    .GroupBy(c => c.SemesterId)
                    .Select(g => new { SemesterId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.SemesterId!.Value, x => x.Count);

                var scheduleSemesterIds = await _scheduleRepository.FindAll()
                    .Where(cs => !cs.IsDeleted && cs.Class != null && !cs.Class.IsDeleted && cs.Class.SemesterId != null && semesterIds.Contains(cs.Class.SemesterId.Value))
                    .Select(cs => cs.Class.SemesterId!.Value)
                    .Distinct()
                    .ToListAsync();

                var dtos = entities.Select(e => {
                    var dto = MapToDto(e);
                    dto.ClassCount = classCounts.ContainsKey(e.Id) ? classCounts[e.Id] : 0;
                    dto.HasSchedules = scheduleSemesterIds.Contains(e.Id);
                    return dto;
                }).ToList();

                return ApiResponse<List<SemesterDto>>.Ok(dtos, "GET_SEMESTER_LIST_SUCCESS");
            }
            catch (Exception)
            {
                return ApiResponse<List<SemesterDto>>.Fail("ERR_SYSTEM_ERROR", StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<SemesterDto>> GetByIdAsync(int id)
        {
            try
            {
                var entity = await _semesterRepository.GetByIdAsync(id);
                if (entity == null || entity.IsDeleted)
                {
                    return ApiResponse<SemesterDto>.Fail("ERR_SEMESTER_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                var classCount = await _classRepository.FindAll().CountAsync(c => c.SemesterId == id && !c.IsDeleted);
                var hasSchedules = await _scheduleRepository.FindAll().AnyAsync(cs => cs.Class.SemesterId == id && !cs.Class.IsDeleted && !cs.IsDeleted);

                var dto = MapToDto(entity);
                dto.ClassCount = classCount;
                dto.HasSchedules = hasSchedules;

                return ApiResponse<SemesterDto>.Ok(dto, "GET_SEMESTER_DETAIL_SUCCESS");
            }
            catch (Exception)
            {
                return ApiResponse<SemesterDto>.Fail("ERR_SYSTEM_ERROR", StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<SemesterDto>> CreateAsync(SemesterSaveDto dto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dto.Code) || string.IsNullOrWhiteSpace(dto.Name))
                {
                    return ApiResponse<SemesterDto>.Fail("ERR_SEMESTER_CODE_NAME_REQUIRED", StatusCodes.Status400BadRequest);
                }
                
                var (codeExists, nameExists) = await ValidationHelper.CheckDuplicateCodeAndNameAsync(_semesterRepository, null, dto.Code, dto.Name);
                if (codeExists)
                {
                    return ApiResponse<SemesterDto>.Fail("ERR_SEMESTER_CODE_EXISTS", StatusCodes.Status400BadRequest);
                }
                if (nameExists)
                {
                    return ApiResponse<SemesterDto>.Fail("ERR_SEMESTER_NAME_EXISTS", StatusCodes.Status400BadRequest);
                }
                
                if (dto.EndDate < dto.StartDate)
                {
                    return ApiResponse<SemesterDto>.Fail("ERR_SEMESTER_END_DATE_BEFORE_START_DATE", StatusCodes.Status400BadRequest);
                }
                
                if (dto.StartDate.AddMonths(1) > dto.EndDate)
                {
                    return ApiResponse<SemesterDto>.Fail("ERR_SEMESTER_DURATION_MIN_ONE_MONTH", StatusCodes.Status400BadRequest);
                }
                
                if (dto.EndDate > dto.StartDate.AddMonths(3))
                {
                    return ApiResponse<SemesterDto>.Fail("ERR_SEMESTER_DURATION_MAX_THREE_MONTHS", StatusCodes.Status400BadRequest);
                }
                
                var entity = new Semester
                {
                    Code = dto.Code,
                    Name = dto.Name,
                    StartDate = dto.StartDate,
                    EndDate = dto.EndDate,
                    Status = dto.Status != 0 ? dto.Status : (int)SemesterStatus.Active,
                    TextSearch = dto.TextSearch
                };
                
                await _semesterRepository.AddAsync(entity);
                await _semesterRepository.SaveChangesAsync();
                
                var result = MapToDto(entity);
                result.ClassCount = 0;
                result.HasSchedules = false;
                
                return ApiResponse<SemesterDto>.Created(result, "CREATE_SEMESTER_SUCCESS");
            }
            catch (Exception)
            {
                return ApiResponse<SemesterDto>.Fail("ERR_SYSTEM_ERROR", StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<SemesterDto>> EditAsync(SemesterSaveDto dto)
        {
            try
            {
                var entity = await _semesterRepository.GetByIdAsync(dto.Id);
                if (entity == null || entity.IsDeleted)
                {
                    return ApiResponse<SemesterDto>.Fail("ERR_SEMESTER_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                var classCount = await _classRepository.FindAll().CountAsync(c => c.SemesterId == dto.Id && !c.IsDeleted);
                var hasSchedules = await _scheduleRepository.FindAll().AnyAsync(cs => cs.Class.SemesterId == dto.Id && !cs.Class.IsDeleted && !cs.IsDeleted);

                var (codeExists, nameExists) = await ValidationHelper.CheckDuplicateCodeAndNameAsync(_semesterRepository, dto.Id, dto.Code, dto.Name);
                if (codeExists)
                {
                    return ApiResponse<SemesterDto>.Fail("ERR_SEMESTER_CODE_EXISTS", StatusCodes.Status400BadRequest);
                }
                if (nameExists)
                {
                    return ApiResponse<SemesterDto>.Fail("ERR_SEMESTER_NAME_EXISTS", StatusCodes.Status400BadRequest);
                }

                if (hasSchedules)
                {
                    if (entity.StartDate != dto.StartDate || entity.EndDate != dto.EndDate)
                    {
                        return ApiResponse<SemesterDto>.Fail("ERR_SEMESTER_HAS_SCHEDULES_CANNOT_CHANGE_DATES", StatusCodes.Status400BadRequest);
                    }
                }
                else
                {
                    if (dto.EndDate < dto.StartDate)
                    {
                        return ApiResponse<SemesterDto>.Fail("ERR_SEMESTER_END_DATE_BEFORE_START_DATE", StatusCodes.Status400BadRequest);
                    }

                    if (dto.StartDate.AddMonths(1) > dto.EndDate)
                    {
                        return ApiResponse<SemesterDto>.Fail("ERR_SEMESTER_DURATION_MIN_ONE_MONTH", StatusCodes.Status400BadRequest);
                    }

                    if (dto.EndDate > dto.StartDate.AddMonths(3))
                    {
                        return ApiResponse<SemesterDto>.Fail("ERR_SEMESTER_DURATION_MAX_THREE_MONTHS", StatusCodes.Status400BadRequest);
                    }
                }

                entity.Code = dto.Code;
                entity.Name = dto.Name;
                if (!hasSchedules)
                {
                    entity.StartDate = dto.StartDate;
                    entity.EndDate = dto.EndDate;
                }
                entity.Status = dto.Status != 0 ? dto.Status : 1; // Always fallback to active if not provided or 0
                entity.TextSearch = dto.TextSearch;

                await _semesterRepository.UpdateAsync(entity);
                await _semesterRepository.SaveChangesAsync();

                var result = MapToDto(entity);
                result.ClassCount = classCount;
                result.HasSchedules = hasSchedules;

                return ApiResponse<SemesterDto>.Ok(result, "UPDATE_SEMESTER_SUCCESS");
            }
            catch (Exception)
            {
                return ApiResponse<SemesterDto>.Fail("ERR_SYSTEM_ERROR", StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<bool>> DeleteAsync(int id)
        {
            try
            {
                var entity = await _semesterRepository.GetByIdAsync(id);
                if (entity == null || entity.IsDeleted)
                {
                    return ApiResponse<bool>.Fail("ERR_SEMESTER_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                var hasClasses = await _classRepository.FindAll()
                    .AnyAsync(c => c.SemesterId == id && !c.IsDeleted);
                if (hasClasses)
                {
                    return ApiResponse<bool>.Fail("ERR_SEMESTER_HAS_CLASSES", StatusCodes.Status400BadRequest);
                }

                await _semesterRepository.DeleteAsync(entity);
                await _semesterRepository.SaveChangesAsync();

                return ApiResponse<bool>.Ok(true, "DELETE_SEMESTER_SUCCESS");
            }
            catch (Exception)
            {
                return ApiResponse<bool>.Fail("ERR_SYSTEM_ERROR", StatusCodes.Status500InternalServerError);
            }
        }

        // ==================== TEACHER AVAILABILITY ====================

        public async Task<ApiResponse<List<TeacherAvailabilityDto>>> GetTeacherAvailabilitiesAsync(int semesterId, int teacherId)
        {
            try
            {
                var list = await _availabilityRepository.FindAll()
                    .Include(t => t.Teacher)
                    .Include(t => t.Semester)
                    .Where(t => t.SemesterId == semesterId && t.TeacherId == teacherId)
                    .ToListAsync();

                var fixedSlots = FixedTimeSlot.All;
                var dtos = list.Select(x => new TeacherAvailabilityDto
                {
                    Id = x.Id,
                    TeacherId = x.TeacherId,
                    TeacherName = x.Teacher?.Name,
                    TeacherCode = x.Teacher?.Code,
                    SemesterId = x.SemesterId,
                    SemesterName = x.Semester?.Name,
                    DayOfWeek = x.DayOfWeek,
                    SlotIndex = x.SlotIndex,
                    SlotName = x.SlotIndex >= 0 && x.SlotIndex < fixedSlots.Length ? fixedSlots[x.SlotIndex].Name : $"Ca {x.SlotIndex + 1}"
                }).ToList();

                return ApiResponse<List<TeacherAvailabilityDto>>.Ok(dtos, "GET_TEACHER_AVAILABILITY_SUCCESS");
            }
            catch (Exception)
            {
                return ApiResponse<List<TeacherAvailabilityDto>>.Fail("ERR_SYSTEM_ERROR", StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<List<TeacherAvailabilityDto>>> GetAllTeacherAvailabilitiesAsync(int semesterId)
        {
            try
            {
                var list = await _availabilityRepository.FindAll()
                    .Include(t => t.Teacher)
                    .Include(t => t.Semester)
                    .Where(t => t.SemesterId == semesterId)
                    .ToListAsync();

                var fixedSlots = FixedTimeSlot.All;
                var dtos = list.Select(x => new TeacherAvailabilityDto
                {
                    Id = x.Id,
                    TeacherId = x.TeacherId,
                    TeacherName = x.Teacher?.Name,
                    TeacherCode = x.Teacher?.Code,
                    SemesterId = x.SemesterId,
                    SemesterName = x.Semester?.Name,
                    DayOfWeek = x.DayOfWeek,
                    SlotIndex = x.SlotIndex,
                    SlotName = x.SlotIndex >= 0 && x.SlotIndex < fixedSlots.Length ? fixedSlots[x.SlotIndex].Name : $"Ca {x.SlotIndex + 1}"
                }).ToList();

                return ApiResponse<List<TeacherAvailabilityDto>>.Ok(dtos, "GET_ALL_TEACHER_AVAILABILITIES_SUCCESS");
            }
            catch (Exception)
            {
                return ApiResponse<List<TeacherAvailabilityDto>>.Fail("ERR_SYSTEM_ERROR", StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<bool>> SaveTeacherAvailabilityAsync(TeacherAvailabilitySaveDto dto)
        {
            using var transaction = await _semesterRepository.BeginTransactionAsync();
            try
            {
                var hasSchedules = await _scheduleRepository.FindAll().AnyAsync(cs =>
                    cs.Class.SemesterId == dto.SemesterId &&
                    cs.TeacherId == dto.TeacherId &&
                    !cs.IsDeleted &&
                    !cs.Class.IsDeleted);

                if (hasSchedules)
                {
                    return ApiResponse<bool>.Fail("ERR_TEACHER_ALREADY_SCHEDULED_CANNOT_CHANGE_AVAILABILITY", StatusCodes.Status400BadRequest);
                }

                // Clear existing availabilities for this teacher and semester
                var existing = await _availabilityRepository.FindAll()
                    .Where(t => t.SemesterId == dto.SemesterId && t.TeacherId == dto.TeacherId)
                    .ToListAsync();
                await _availabilityRepository.DeleteRangeAsync(existing);

                // Add new slots
                if (dto.Slots != null && dto.Slots.Any())
                {
                    var fixedSlots = FixedTimeSlot.All;
                    foreach (var slot in dto.Slots)
                    {
                        if (slot.DayOfWeek < 0 || slot.DayOfWeek > 6 || slot.SlotIndex < 0 || slot.SlotIndex >= fixedSlots.Length)
                        {
                            await transaction.RollbackAsync();
                            return ApiResponse<bool>.Fail("ERR_INVALID_DAY_OR_SLOT", StatusCodes.Status400BadRequest);
                        }

                        await _availabilityRepository.AddAsync(new TeacherAvailability
                        {
                            TeacherId = dto.TeacherId,
                            SemesterId = dto.SemesterId,
                            DayOfWeek = slot.DayOfWeek,
                            SlotIndex = slot.SlotIndex
                        });
                    }
                }

                await _semesterRepository.SaveChangesAsync();
                await transaction.CommitAsync();

                return ApiResponse<bool>.Ok(true, "SAVE_TEACHER_AVAILABILITY_SUCCESS");
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return ApiResponse<bool>.Fail("ERR_SYSTEM_ERROR", StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<bool>> SaveTeacherAvailabilitiesBulkAsync(List<TeacherAvailabilitySaveDto> dtos)
        {
            if (dtos == null || !dtos.Any())
            {
                return ApiResponse<bool>.Fail("ERR_NO_DATA_TO_SAVE", StatusCodes.Status400BadRequest);
            }

            using var transaction = await _semesterRepository.BeginTransactionAsync();
            try
            {
                var semesterId = dtos.First().SemesterId;
                var semester = await _semesterRepository.GetByIdAsync(semesterId);
                if (semester == null || semester.IsDeleted)
                {
                    return ApiResponse<bool>.Fail("ERR_SEMESTER_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                // Check teachers with schedules in this semester to lock them
                var teachersWithSchedules = await _scheduleRepository.FindAll()
                    .Where(cs => cs.Class.SemesterId == semesterId && !cs.IsDeleted && !cs.Class.IsDeleted)
                    .Select(cs => cs.TeacherId)
                    .Distinct()
                    .ToListAsync();

                var fixedSlots = FixedTimeSlot.All;

                foreach (var dto in dtos)
                {
                    // Skip if teacher has schedule (availability is locked)
                    if (teachersWithSchedules.Contains(dto.TeacherId))
                    {
                        continue;
                    }

                    // Clear existing availabilities for this teacher and semester
                    var existing = await _availabilityRepository.FindAll()
                        .Where(t => t.SemesterId == semesterId && t.TeacherId == dto.TeacherId)
                        .ToListAsync();
                    await _availabilityRepository.DeleteRangeAsync(existing);

                    // Add new slots
                    if (dto.Slots != null && dto.Slots.Any())
                    {
                        foreach (var slot in dto.Slots)
                        {
                            if (slot.DayOfWeek < 0 || slot.DayOfWeek > 6 || slot.SlotIndex < 0 || slot.SlotIndex >= fixedSlots.Length)
                            {
                                continue;
                            }

                            await _availabilityRepository.AddAsync(new TeacherAvailability
                            {
                                TeacherId = dto.TeacherId,
                                SemesterId = semesterId,
                                DayOfWeek = slot.DayOfWeek,
                                SlotIndex = slot.SlotIndex
                            });
                        }
                    }
                }

                await _semesterRepository.SaveChangesAsync();
                await transaction.CommitAsync();

                return ApiResponse<bool>.Ok(true, "SAVE_TEACHER_AVAILABILITY_SUCCESS");
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return ApiResponse<bool>.Fail("ERR_SYSTEM_ERROR", StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<bool>> CheckTeacherHasSchedulesAsync(int semesterId, int teacherId)
        {
            try
            {
                var hasSchedules = await _scheduleRepository.FindAll().AnyAsync(cs =>
                    cs.Class.SemesterId == semesterId &&
                    cs.TeacherId == teacherId &&
                    !cs.IsDeleted &&
                    !cs.Class.IsDeleted);

                return ApiResponse<bool>.Ok(hasSchedules, "CHECK_TEACHER_SCHEDULES_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        // ==================== STUDENT REGISTRATION ====================

        public async Task<ApiResponse<List<StudentRegistrationDto>>> GetStudentRegistrationsAsync(int semesterId)
        {
            try
            {
                var list = await _studentRegistrationRepository.GetRegistrationsWithDetails()
                    .Where(sr => sr.SemesterId == semesterId)
                    .OrderByDescending(sr => sr.Id)
                    .ToListAsync();

                var dtos = list.Select(MapRegistrationToDto).ToList();
                return ApiResponse<List<StudentRegistrationDto>>.Ok(dtos, "GET_STUDENT_REGISTRATION_SUCCESS");
            }
            catch (Exception)
            {
                return ApiResponse<List<StudentRegistrationDto>>.Fail("ERR_SYSTEM_ERROR", StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<PagingResponse<StudentRegistrationDto>>> GetStudentRegistrationsPagedAsync(
            int semesterId, string? keyword, int? courseId, int? status, int pageIndex, int pageSize)
        {
            try
            {
                var query = _studentRegistrationRepository.GetRegistrationsWithDetails();

                if (semesterId > 0)
                {
                    query = query.Where(sr => sr.SemesterId == semesterId);
                }

                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    var keywordLower = keyword.ToLower();
                    query = query.Where(sr =>
                        (sr.Student != null && sr.Student.Name != null && sr.Student.Name.ToLower().Contains(keywordLower)) ||
                        (sr.Student != null && sr.Student.Code != null && sr.Student.Code.ToLower().Contains(keywordLower)) ||
                        (sr.Student != null && sr.Student.Email != null && sr.Student.Email.ToLower().Contains(keywordLower)) ||
                        (sr.Student != null && sr.Student.Phone != null && sr.Student.Phone.ToLower().Contains(keywordLower)) ||
                        (sr.Course != null && sr.Course.Name != null && sr.Course.Name.ToLower().Contains(keywordLower))
                    );
                }

                if (courseId.HasValue && courseId.Value > 0)
                {
                    query = query.Where(sr => sr.CourseId == courseId.Value);
                }

                if (status.HasValue)
                {
                    query = query.Where(sr => sr.Status == status.Value);
                }

                query = query.OrderByDescending(sr => sr.Id);

                var totalRecords = await query.CountAsync();
                var entities = await query.ApplyPagingAsync(pageIndex, pageSize);

                var dtos = entities.Select(MapRegistrationToDto).ToList();
                var pagingResponse = dtos.ToPagingResponse(totalRecords, pageIndex, pageSize);

                return ApiResponse<PagingResponse<StudentRegistrationDto>>.Ok(pagingResponse, "GET_STUDENT_REGISTRATION_SUCCESS");
            }
            catch (Exception)
            {
                return ApiResponse<PagingResponse<StudentRegistrationDto>>.Fail("ERR_SYSTEM_ERROR", StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<List<StudentRegistrationDto>>> ImportStudentRegistrationsAsync(List<StudentRegistrationSaveDto> dtos)
        {
            using var transaction = await _studentRegistrationRepository.BeginTransactionAsync();
            try
            {
                var resultDtos = new List<StudentRegistrationDto>();
                var errors = new List<string>();

                foreach (var dto in dtos)
                {
                    try
                    {
                        if (dto.SemesterId == 0 || string.IsNullOrWhiteSpace(dto.StudentEmail))
                        {
                            errors.Add("ERR_REGISTRATION_MISSING_SEMESTER_OR_EMAIL");
                            continue;
                        }

                        // 0. Auto-resolve Course: if CourseId == 0 but CourseName provided, find or create course
                        if (dto.CourseId == 0)
                        {
                            if (string.IsNullOrWhiteSpace(dto.CourseName))
                            {
                                errors.Add("ERR_REGISTRATION_MISSING_COURSE");
                                continue;
                            }

                            var courseName = dto.CourseName.Trim();
                            var course = await _courseRepository.FindAll()
                                .FirstOrDefaultAsync(c => !c.IsDeleted && c.Name != null 
                                    && c.Name.ToLower() == courseName.ToLower());

                            if (course == null)
                            {
                                // Auto-create course
                                var courseCode = await GenerateCourseCodeAsync();
                                course = new Course
                                {
                                    Code = courseCode,
                                    Name = courseName,
                                    Status = 1,
                                    TextSearch = StringHelper.GenerateTextSearch(courseCode, courseName)
                                };
                                await _courseRepository.AddAsync(course);
                                await _courseRepository.SaveChangesAsync();
                            }

                            dto.CourseId = course.Id;
                        }

                        // 1. Find or create Student
                        var student = await _studentRepository.FindAll()
                            .FirstOrDefaultAsync(s => s.Email == dto.StudentEmail && !s.IsDeleted);
                        
                        if (student == null)
                        {
                            // Auto create student profile
                            var studentCode = !string.IsNullOrWhiteSpace(dto.StudentCode) 
                                ? dto.StudentCode 
                                : $"ST_{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";

                            student = new Student
                            {
                                Code = studentCode,
                                Name = dto.StudentName,
                                Email = dto.StudentEmail,
                                Phone = dto.StudentPhone,
                                Status = (int)StudentStatus.Active,
                                TextSearch = StringHelper.GenerateTextSearch(studentCode, dto.StudentName, dto.StudentEmail)
                            };

                            await _studentRepository.AddAsync(student);
                            await _studentRepository.SaveChangesAsync();
                        }

                        // 2. Clear existing registration for this student/semester/course to avoid duplication
                        var existing = await _studentRegistrationRepository.GetRegistrationByStudentCourseSemesterAsync(student.Id, dto.CourseId, dto.SemesterId);

                        if (existing != null)
                        {
                            // Overwrite or update
                            existing.PreferredSlotsJson = JsonSerializer.Serialize(dto.PreferredSlots ?? new List<string>());
                            existing.PreferredSlotIndex = dto.PreferredSlotIndex;
                            existing.PreferredDaysOfWeek = dto.PreferredDaysOfWeek;
                            existing.Status = (int)StudentRegistrationStatus.Pending;
                            existing.EnrollType = dto.EnrollType;
                            await _studentRegistrationRepository.UpdateAsync(existing);
                            await _studentRegistrationRepository.SaveChangesAsync();

                            var reloaded = await _studentRegistrationRepository.GetRegistrationWithDetailsByIdAsync(existing.Id);

                            if (reloaded != null) resultDtos.Add(MapRegistrationToDto(reloaded));
                        }
                        else
                        {
                            var reg = new StudentRegistration
                            {
                                StudentId = student.Id,
                                CourseId = dto.CourseId,
                                SemesterId = dto.SemesterId,
                                PreferredSlotsJson = JsonSerializer.Serialize(dto.PreferredSlots ?? new List<string>()),
                                PreferredSlotIndex = dto.PreferredSlotIndex,
                                PreferredDaysOfWeek = dto.PreferredDaysOfWeek,
                                Status = (int)StudentRegistrationStatus.Pending,
                                EnrollType = dto.EnrollType
                            };

                            await _studentRegistrationRepository.AddAsync(reg);
                            await _studentRegistrationRepository.SaveChangesAsync();

                            var reloaded = await _studentRegistrationRepository.GetRegistrationWithDetailsByIdAsync(reg.Id);

                            if (reloaded != null) resultDtos.Add(MapRegistrationToDto(reloaded));
                        }
                    }
                    catch (Exception)
                    {
                        errors.Add("ERR_REGISTRATION_ROW_ERROR");
                    }
                }

                if (errors.Any())
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<List<StudentRegistrationDto>>.Fail(string.Join("; ", errors), StatusCodes.Status400BadRequest);
                }

                await _studentRegistrationRepository.SaveChangesAsync();
                await transaction.CommitAsync();

                return ApiResponse<List<StudentRegistrationDto>>.Ok(resultDtos, "IMPORT_STUDENT_REGISTRATION_SUCCESS");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return ApiResponse<List<StudentRegistrationDto>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<StudentRegistrationDto>> CreateStudentRegistrationAsync(StudentRegistrationSaveDto dto)
        {
            try
            {
                if (dto.SemesterId == 0 || string.IsNullOrWhiteSpace(dto.StudentEmail))
                {
                    return ApiResponse<StudentRegistrationDto>.Fail("ERR_REGISTRATION_MISSING_SEMESTER_OR_EMAIL", StatusCodes.Status400BadRequest);
                }

                if (dto.CourseId == 0)
                {
                    return ApiResponse<StudentRegistrationDto>.Fail("ERR_REGISTRATION_MISSING_COURSE", StatusCodes.Status400BadRequest);
                }

                // 1. Find or create Student
                var student = await _studentRepository.FindAll()
                    .FirstOrDefaultAsync(s => s.Email == dto.StudentEmail && !s.IsDeleted);
                
                if (student == null)
                {
                    var studentCode = !string.IsNullOrWhiteSpace(dto.StudentCode) 
                        ? dto.StudentCode 
                        : $"ST_{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";

                    student = new Student
                    {
                        Code = studentCode,
                        Name = dto.StudentName,
                        Email = dto.StudentEmail,
                        Phone = dto.StudentPhone,
                        Status = (int)StudentStatus.Active,
                        TextSearch = StringHelper.GenerateTextSearch(studentCode, dto.StudentName, dto.StudentEmail)
                    };

                    await _studentRepository.AddAsync(student);
                    await _studentRepository.SaveChangesAsync();
                }

                // 2. Check existing registration for this student/semester/course
                var existing = await _studentRegistrationRepository.GetRegistrationByStudentCourseSemesterAsync(student.Id, dto.CourseId, dto.SemesterId);

                if (existing != null)
                {
                    return ApiResponse<StudentRegistrationDto>.Fail("ERR_STUDENT_ALREADY_REGISTERED_FOR_THIS_COURSE", StatusCodes.Status400BadRequest);
                }

                // 3. Create new StudentRegistration
                var reg = new StudentRegistration
                {
                    StudentId = student.Id,
                    CourseId = dto.CourseId,
                    SemesterId = dto.SemesterId,
                    PreferredSlotsJson = JsonSerializer.Serialize(dto.PreferredSlots ?? new List<string>()),
                    PreferredSlotIndex = dto.PreferredSlotIndex,
                    PreferredDaysOfWeek = dto.PreferredDaysOfWeek,
                    Status = dto.Status,
                    EnrollType = dto.EnrollType
                };

                await _studentRegistrationRepository.AddAsync(reg);
                await _studentRegistrationRepository.SaveChangesAsync();

                var reloaded = await _studentRegistrationRepository.GetRegistrationWithDetailsByIdAsync(reg.Id);

                return ApiResponse<StudentRegistrationDto>.Created(MapRegistrationToDto(reloaded!), "CREATE_REGISTRATION_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<StudentRegistrationDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<StudentRegistrationDto>> EditStudentRegistrationAsync(int id, StudentRegistrationSaveDto dto)
        {
            try
            {
                var existing = await _studentRegistrationRepository.GetByIdAsync(id);

                if (existing == null)
                {
                    return ApiResponse<StudentRegistrationDto>.Fail("ERR_REGISTRATION_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                if (existing.Status == 1) // 1 = Scheduled
                {
                    return ApiResponse<StudentRegistrationDto>.Fail("ERR_REGISTRATION_ALREADY_SCHEDULED_CANNOT_MODIFY", StatusCodes.Status400BadRequest);
                }

                // Update details
                existing.CourseId = dto.CourseId;
                existing.SemesterId = dto.SemesterId;
                existing.PreferredSlotsJson = JsonSerializer.Serialize(dto.PreferredSlots ?? new List<string>());
                existing.PreferredSlotIndex = dto.PreferredSlotIndex;
                existing.PreferredDaysOfWeek = dto.PreferredDaysOfWeek;
                existing.Status = dto.Status;
                existing.EnrollType = dto.EnrollType;

                await _studentRegistrationRepository.UpdateAsync(existing);
                await _studentRegistrationRepository.SaveChangesAsync();

                var reloaded = await _studentRegistrationRepository.GetRegistrationWithDetailsByIdAsync(existing.Id);

                return ApiResponse<StudentRegistrationDto>.Ok(MapRegistrationToDto(reloaded!), "UPDATE_REGISTRATION_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<StudentRegistrationDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<bool>> DeleteStudentRegistrationAsync(int id)
        {
            try
            {
                var existing = await _studentRegistrationRepository.GetByIdAsync(id);

                if (existing == null)
                {
                    return ApiResponse<bool>.Fail("ERR_REGISTRATION_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                if (existing.Status == 1) // 1 = Scheduled
                {
                    return ApiResponse<bool>.Fail("ERR_REGISTRATION_ALREADY_SCHEDULED_CANNOT_DELETE", StatusCodes.Status400BadRequest);
                }

                await _studentRegistrationRepository.DeleteAsync(existing);
                await _studentRegistrationRepository.SaveChangesAsync();

                return ApiResponse<bool>.Ok(true, "DELETE_REGISTRATION_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<bool>> DeleteStudentRegistrationsAsync(List<int> ids)
        {
            try
            {
                var failedIds = new List<int>();
                var successfulItems = new List<StudentRegistration>();

                foreach (var id in ids)
                {
                    var existing = await _studentRegistrationRepository.GetByIdAsync(id);
                    if (existing == null || existing.Status == 1) // Scheduled cannot be deleted
                    {
                        failedIds.Add(id);
                        continue;
                    }
                    successfulItems.Add(existing);
                }

                if (successfulItems.Count == 0)
                {
                    return ApiResponse<bool>.Fail("ERR_NO_REGISTRATIONS_COULD_BE_DELETED", StatusCodes.Status400BadRequest);
                }

                foreach (var item in successfulItems)
                {
                    await _studentRegistrationRepository.DeleteAsync(item);
                }
                await _studentRegistrationRepository.SaveChangesAsync();

                if (failedIds.Count > 0)
                {
                    return ApiResponse<bool>.Ok(true, "DELETE_REGISTRATIONS_PARTIAL_SUCCESS");
                }

                return ApiResponse<bool>.Ok(true, "DELETE_REGISTRATIONS_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        // ==================== MAPPERS ====================

        private static SemesterDto MapToDto(Semester entity) => new SemesterDto
        {
            Id = entity.Id,
            Code = entity.Code,
            Name = entity.Name,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            Status = entity.Status,
            StatusName = ((SemesterStatus)entity.Status).GetStringValue()
        };

        private static StudentRegistrationDto MapRegistrationToDto(StudentRegistration entity)
        {
            List<string> preferredSlots;
            try
            {
                preferredSlots = JsonSerializer.Deserialize<List<string>>(entity.PreferredSlotsJson ?? "[]") ?? new List<string>();
            }
            catch
            {
                preferredSlots = new List<string>();
            }

            return new StudentRegistrationDto
            {
                Id = entity.Id,
                StudentId = entity.StudentId,
                StudentCode = entity.Student?.Code,
                StudentName = entity.Student?.Name,
                StudentEmail = entity.Student?.Email,
                StudentPhone = entity.Student?.Phone,
                CourseId = entity.CourseId,
                CourseName = entity.Course?.Name,
                SemesterId = entity.SemesterId,
                SemesterName = entity.Semester?.Name,
                PreferredSlots = preferredSlots,
                PreferredSlotIndex = entity.PreferredSlotIndex,
                PreferredDaysOfWeek = entity.PreferredDaysOfWeek,
                Status = entity.Status,
                StatusName = ((StudentRegistrationStatus)entity.Status).GetStringValue(),
                EnrollType = entity.EnrollType,
                EnrollTypeName = entity.EnrollType == 1 ? "Online" : "Offline",
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
        }

        // ==================== HELPERS ====================

        private async Task<string> GenerateCourseCodeAsync()
        {
            var maxCourse = await _courseRepository.FindAll()
                .Where(c => !c.IsDeleted && c.Code != null && c.Code.StartsWith("KH"))
                .OrderByDescending(c => c.Code)
                .FirstOrDefaultAsync();

            if (maxCourse != null && maxCourse.Code != null && maxCourse.Code.Length > 2)
            {
                var numStr = maxCourse.Code.Substring(2);
                if (int.TryParse(numStr, out int num))
                {
                    return $"KH{(num + 1):D5}";
                }
            }
            return "KH00001";
        }
    }
}

