using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using sep490_be.Services.Interfaces;
using sep490_be.Models;
using System.Threading.Tasks;

namespace sep490_be.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestController : ControllerBase
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IClassService _classService;
        private readonly IStudentGradeService _gradeService;
        private readonly ApplicationDbContext _dbContext;

        public TestController(
            UserManager<IdentityUser> userManager,
            IClassService classService,
            IStudentGradeService gradeService,
            ApplicationDbContext dbContext)
        {
            _userManager = userManager;
            _classService = classService;
            _gradeService = gradeService;
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            return Ok("Heheh123");
        }

        [HttpGet("reset-vanc")]
        public async Task<IActionResult> ResetVanC()
        {
            var user = await _userManager.FindByEmailAsync("vanc@gmail.com");
            if (user == null)
            {
                user = await _userManager.FindByNameAsync("vanc");
            }

            if (user == null)
            {
                return NotFound("vanc user not found in identity");
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, "123456");
            if (!result.Succeeded)
            {
                return BadRequest(result.Errors);
            }

            // Check schedules
            var schedulesResp = await _classService.GetStudentSchedulesAsync(user.UserName ?? "vanc");
            var gradesResp = await _gradeService.GetMyGradesAsync(new[] { "vanc@gmail.com" });

            return Ok(new
            {
                PasswordReset = "Success",
                Username = user.UserName,
                Email = user.Email,
                SchedulesCount = schedulesResp.Data?.Count ?? 0,
                Schedules = schedulesResp.Data,
                Grades = gradesResp.Data
            });
        }
        [HttpGet("debug-attendance")]
        public async Task<IActionResult> DebugAttendance()
        {
            // Check student 26 directly
            var student = await _dbContext.Students
                .AsNoTracking()
                .Where(s => s.Email == "vanc@gmail.com")
                .FirstOrDefaultAsync();

            if (student == null) return NotFound("Student not found for vanc@gmail.com");

            // Check attendances for student
            var attendances = await _dbContext.Attendances
                .AsNoTracking()
                .Where(a => a.StudentId == student.Id && !a.IsDeleted)
                .Select(a => new { a.Id, a.ScheduleId, a.StudentId, a.Status })
                .ToListAsync();

            // Check schedules for class 53
            var schedules = await _dbContext.ClassSchedules
                .AsNoTracking()
                .Where(cs => cs.ClassId == 53 && !cs.IsDeleted)
                .Select(cs => new { cs.Id, cs.LessonNo, cs.ScheduleDate })
                .OrderBy(cs => cs.ScheduleDate)
                .Take(10)
                .ToListAsync();

            var scheduleIds = schedules.Select(s => s.Id).ToList();
            var attDict = attendances
                .Where(a => a.ScheduleId.HasValue && scheduleIds.Contains(a.ScheduleId.Value))
                .ToDictionary(a => a.ScheduleId!.Value, a => a.Status);

            return Ok(new
            {
                StudentId = student.Id,
                StudentName = student.Name,
                TotalAttendances = attendances.Count,
                AttendancesInClass53Schedules = attDict,
                Schedules = schedules,
                Attendances = attendances
            });
        }
    }
}

