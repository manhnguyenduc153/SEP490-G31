using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using PRN232_be.DTO;
using PRN232_be.DTO.Semester;
using PRN232_be.DTO.Teacher;
using PRN232_be.DTO.Student;
using PRN232_be.Models;
using PRN232_be.Services.Interfaces;
using PRN232_be.Enums;
using PRN232_be.Helpers;

namespace PRN232_be.Services.Implementations
{
    public class SemesterService : ISemesterService
    {
        private readonly ApplicationDbContext _dbContext;

        public SemesterService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<ApiResponse<List<SemesterDto>>> GetAllAsync()
        {
            try
            {
                var entities = await _dbContext.Semesters
                    .Where(s => !s.IsDeleted)
                    .OrderByDescending(s => s.StartDate)
                    .ToListAsync();

                var dtos = entities.Select(MapToDto).ToList();
                return ApiResponse<List<SemesterDto>>.Ok(dtos, "GET_SEMESTER_LIST_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<List<SemesterDto>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<SemesterDto>> GetByIdAsync(int id)
        {
            try
            {
                var entity = await _dbContext.Semesters.FindAsync(id);
                if (entity == null || entity.IsDeleted)
                {
                    return ApiResponse<SemesterDto>.Fail("ERR_SEMESTER_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                return ApiResponse<SemesterDto>.Ok(MapToDto(entity), "GET_SEMESTER_DETAIL_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<SemesterDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
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

                // Check existing active semester with same code
                var existing = await _dbContext.Semesters.FirstOrDefaultAsync(s => s.Code == dto.Code && !s.IsDeleted);
                if (existing != null)
                {
                    return ApiResponse<SemesterDto>.Fail("ERR_SEMESTER_CODE_EXISTS", StatusCodes.Status400BadRequest);
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

                _dbContext.Semesters.Add(entity);
                await _dbContext.SaveChangesAsync();

                return ApiResponse<SemesterDto>.Created(MapToDto(entity), "CREATE_SEMESTER_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<SemesterDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<SemesterDto>> EditAsync(SemesterSaveDto dto)
        {
            try
            {
                var entity = await _dbContext.Semesters.FindAsync(dto.Id);
                if (entity == null || entity.IsDeleted)
                {
                    return ApiResponse<SemesterDto>.Fail("ERR_SEMESTER_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                entity.Code = dto.Code;
                entity.Name = dto.Name;
                entity.StartDate = dto.StartDate;
                entity.EndDate = dto.EndDate;
                entity.Status = dto.Status;
                entity.TextSearch = dto.TextSearch;

                _dbContext.Semesters.Update(entity);
                await _dbContext.SaveChangesAsync();

                return ApiResponse<SemesterDto>.Ok(MapToDto(entity), "UPDATE_SEMESTER_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<SemesterDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<bool>> DeleteAsync(int id)
        {
            try
            {
                var entity = await _dbContext.Semesters.FindAsync(id);
                if (entity == null || entity.IsDeleted)
                {
                    return ApiResponse<bool>.Fail("ERR_SEMESTER_NOT_FOUND", StatusCodes.Status404NotFound);
                }

                entity.IsDeleted = true;
                _dbContext.Semesters.Update(entity);
                await _dbContext.SaveChangesAsync();

                return ApiResponse<bool>.Ok(true, "DELETE_SEMESTER_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        // ==================== TEACHER AVAILABILITY ====================

        public async Task<ApiResponse<List<TeacherAvailabilityDto>>> GetTeacherAvailabilitiesAsync(int semesterId, int teacherId)
        {
            try
            {
                var list = await _dbContext.TeacherAvailabilities
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
                    SemesterId = x.SemesterId,
                    SemesterName = x.Semester?.Name,
                    DayOfWeek = x.DayOfWeek,
                    SlotIndex = x.SlotIndex,
                    SlotName = x.SlotIndex >= 0 && x.SlotIndex < fixedSlots.Length ? fixedSlots[x.SlotIndex].Name : $"Ca {x.SlotIndex + 1}"
                }).ToList();

                return ApiResponse<List<TeacherAvailabilityDto>>.Ok(dtos, "GET_TEACHER_AVAILABILITY_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<List<TeacherAvailabilityDto>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<bool>> SaveTeacherAvailabilityAsync(TeacherAvailabilitySaveDto dto)
        {
            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                // Clear existing availabilities for this teacher and semester
                var existing = await _dbContext.TeacherAvailabilities
                    .Where(t => t.SemesterId == dto.SemesterId && t.TeacherId == dto.TeacherId)
                    .ToListAsync();
                _dbContext.TeacherAvailabilities.RemoveRange(existing);

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

                        _dbContext.TeacherAvailabilities.Add(new TeacherAvailability
                        {
                            TeacherId = dto.TeacherId,
                            SemesterId = dto.SemesterId,
                            DayOfWeek = slot.DayOfWeek,
                            SlotIndex = slot.SlotIndex
                        });
                    }
                }

                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                return ApiResponse<bool>.Ok(true, "SAVE_TEACHER_AVAILABILITY_SUCCESS");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return ApiResponse<bool>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        // ==================== STUDENT REGISTRATION ====================

        public async Task<ApiResponse<List<StudentRegistrationDto>>> GetStudentRegistrationsAsync(int semesterId)
        {
            try
            {
                var list = await _dbContext.StudentRegistrations
                    .Include(sr => sr.Student)
                    .Include(sr => sr.Course)
                    .Include(sr => sr.Semester)
                    .Where(sr => sr.SemesterId == semesterId)
                    .ToListAsync();

                var dtos = list.Select(MapRegistrationToDto).ToList();
                return ApiResponse<List<StudentRegistrationDto>>.Ok(dtos, "GET_STUDENT_REGISTRATION_SUCCESS");
            }
            catch (Exception ex)
            {
                return ApiResponse<List<StudentRegistrationDto>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<List<StudentRegistrationDto>>> ImportStudentRegistrationsAsync(List<StudentRegistrationSaveDto> dtos)
        {
            using var transaction = await _dbContext.Database.BeginTransactionAsync();
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
                            errors.Add($"Học sinh '{dto.StudentName}' bị thiếu thông tin kỳ học hoặc email.");
                            continue;
                        }

                        // 0. Auto-resolve Course: if CourseId == 0 but CourseName provided, find or create course
                        if (dto.CourseId == 0)
                        {
                            if (string.IsNullOrWhiteSpace(dto.CourseName))
                            {
                                errors.Add($"Học sinh '{dto.StudentName}': Thiếu thông tin khóa học (CourseId = 0 và CourseName rỗng).");
                                continue;
                            }

                            var courseName = dto.CourseName.Trim();
                            var course = await _dbContext.Courses
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
                                _dbContext.Courses.Add(course);
                                await _dbContext.SaveChangesAsync();
                            }

                            dto.CourseId = course.Id;
                        }

                        // 1. Find or create Student
                        var student = await _dbContext.Students
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

                            _dbContext.Students.Add(student);
                            await _dbContext.SaveChangesAsync();
                        }

                        // 2. Clear existing registration for this student/semester/course to avoid duplication
                        var existing = await _dbContext.StudentRegistrations
                            .FirstOrDefaultAsync(sr => sr.SemesterId == dto.SemesterId 
                                                    && sr.StudentId == student.Id 
                                                    && sr.CourseId == dto.CourseId);
                        
                        if (existing != null)
                        {
                            // Overwrite or update
                            existing.PreferredSlotsJson = JsonSerializer.Serialize(dto.PreferredSlots ?? new List<string>());
                            existing.Status = (int)StudentRegistrationStatus.Pending;
                            _dbContext.StudentRegistrations.Update(existing);
                            await _dbContext.SaveChangesAsync();

                            var reloaded = await _dbContext.StudentRegistrations
                                .Include(sr => sr.Student)
                                .Include(sr => sr.Course)
                                .Include(sr => sr.Semester)
                                .FirstOrDefaultAsync(sr => sr.Id == existing.Id);

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
                                Status = (int)StudentRegistrationStatus.Pending
                            };

                            _dbContext.StudentRegistrations.Add(reg);
                            await _dbContext.SaveChangesAsync();

                            var reloaded = await _dbContext.StudentRegistrations
                                .Include(sr => sr.Student)
                                .Include(sr => sr.Course)
                                .Include(sr => sr.Semester)
                                .FirstOrDefaultAsync(sr => sr.Id == reg.Id);

                            if (reloaded != null) resultDtos.Add(MapRegistrationToDto(reloaded));
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Học sinh '{dto.StudentName}' (Email: {dto.StudentEmail}) lỗi: {ex.Message}");
                    }
                }

                if (errors.Any())
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<List<StudentRegistrationDto>>.Fail(string.Join("; ", errors), StatusCodes.Status400BadRequest);
                }

                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                return ApiResponse<List<StudentRegistrationDto>>.Ok(resultDtos, "IMPORT_STUDENT_REGISTRATION_SUCCESS");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return ApiResponse<List<StudentRegistrationDto>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
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
                Status = entity.Status,
                StatusName = ((StudentRegistrationStatus)entity.Status).GetStringValue()
            };
        }

        // ==================== HELPERS ====================

        private async Task<string> GenerateCourseCodeAsync()
        {
            var maxCourse = await _dbContext.Courses
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
