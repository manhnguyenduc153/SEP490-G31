using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using sep490_be.Services.Interfaces;
using sep490_be.DTO.Course;
using sep490_be.DTO.Student;
using sep490_be.DTO.Teacher;
using sep490_be.DTO.Room;

namespace sep490_be.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CommonController : ControllerBase
    {
        private readonly ISemesterService _semesterService;
        private readonly ICourseService _courseService;
        private readonly IStudentService _studentService;
        private readonly ITeacherService _teacherService;
        private readonly IRoomService _roomService;

        public CommonController(
            ISemesterService semesterService,
            ICourseService courseService,
            IStudentService studentService,
            ITeacherService teacherService,
            IRoomService roomService)
        {
            _semesterService = semesterService;
            _courseService = courseService;
            _studentService = studentService;
            _teacherService = teacherService;
            _roomService = roomService;
        }

        // GET: api/Common/semesters
        [HttpGet("semesters")]
        public async Task<IActionResult> GetSemesters()
        {
            var response = await _semesterService.GetAllAsync();
            return StatusCode(response.StatusCode, response);
        }

        // GET: api/Common/courses
        [HttpGet("courses")]
        public async Task<IActionResult> GetCourses([FromQuery] CourseSearchDto searchDto)
        {
            var response = await _courseService.GetAllAsync(searchDto);
            return StatusCode(response.StatusCode, response);
        }

        // GET: api/Common/students
        [HttpGet("students")]
        public async Task<IActionResult> GetStudents([FromQuery] StudentSearchDto searchDto)
        {
            var response = await _studentService.GetAllAsync(searchDto);
            return StatusCode(response.StatusCode, response);
        }

        // GET: api/Common/teachers
        [HttpGet("teachers")]
        public async Task<IActionResult> GetTeachers([FromQuery] TeacherSearchDto searchDto)
        {
            var response = await _teacherService.GetAllAsync(searchDto);
            return StatusCode(response.StatusCode, response);
        }

        // GET: api/Common/rooms
        [HttpGet("rooms")]
        public async Task<IActionResult> GetRooms([FromQuery] RoomSearchDto searchDto)
        {
            var response = await _roomService.GetAllAsync(searchDto);
            return StatusCode(response.StatusCode, response);
        }
    }
}
