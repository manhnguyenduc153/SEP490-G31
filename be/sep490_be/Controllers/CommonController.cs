using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using sep490_be.Services.Interfaces;
using sep490_be.DTO.Course;
using sep490_be.DTO.Student;
using sep490_be.DTO.Teacher;
using sep490_be.DTO.Room;
using sep490_be.DTO.Class;
using sep490_be.DTO.QuestionCategory;
using sep490_be.DTO.Common;
using sep490_be.Helpers.Authorization;

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
        private readonly IClassService _classService;
        private readonly IQuestionCategoryService _questionCategoryService;

        public CommonController(
            ISemesterService semesterService,
            ICourseService courseService,
            IStudentService studentService,
            ITeacherService teacherService,
            IRoomService roomService,
            IClassService classService,
            IQuestionCategoryService questionCategoryService)
        {
            _semesterService = semesterService;
            _courseService = courseService;
            _studentService = studentService;
            _teacherService = teacherService;
            _roomService = roomService;
            _classService = classService;
            _questionCategoryService = questionCategoryService;
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
            var response = await _teacherService.GetAllAsync(searchDto, null, true);
            return StatusCode(response.StatusCode, response);
        }

        // GET: api/Common/rooms
        [HttpGet("rooms")]
        public async Task<IActionResult> GetRooms([FromQuery] RoomSearchDto searchDto)
        {
            var response = await _roomService.GetAllAsync(searchDto);
            return StatusCode(response.StatusCode, response);
        }

        // GET: api/Common/classes
        [HttpGet("classes")]
        public async Task<IActionResult> GetClasses([FromQuery] ClassSearchDto searchDto)
        {
            var response = await _classService.GetAllAsync(searchDto);
            return StatusCode(response.StatusCode, response);
        }

        // GET: api/Common/classes/accessible
        [HttpGet("classes/accessible")]
        [Authorize]
        public async Task<IActionResult> GetAccessibleClasses([FromQuery] ClassSearchDto searchDto)
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(username)) return Unauthorized();

            var response = await _classService.GetAccessibleClassesAsync(username, searchDto);
            return StatusCode(response.StatusCode, response);
        }

        // GET: api/Common/question-categories
        [HttpGet("question-categories")]
        public async Task<IActionResult> GetQuestionCategories([FromQuery] QuestionCategorySearchDto searchDto)
        {
            var response = await _questionCategoryService.GetAllAsync(searchDto);
            return StatusCode(response.StatusCode, response);
        }

        // GET: api/Common/teachers/available
        [HttpGet("teachers/available")]
        public async Task<IActionResult> GetAvailableTeachers([FromQuery] AvailableTeacherFilterDto filterDto)
        {
            var response = await _teacherService.GetAvailableTeachersAsync(filterDto);
            return StatusCode(response.StatusCode, response);
        }

        // GET: api/Common/rooms/available
        [HttpGet("rooms/available")]
        public async Task<IActionResult> GetAvailableRooms([FromQuery] AvailableRoomFilterDto filterDto)
        {
            var response = await _roomService.GetAvailableRoomsAsync(filterDto);
            return StatusCode(response.StatusCode, response);
        }

        // PUT: api/Common/schedules/{id}
        [HttpPut("schedules/{id}")]
        public async Task<IActionResult> UpdateScheduleSlot(int id, [FromBody] UpdateScheduleSlotDto dto)
        {
            var response = await _classService.UpdateScheduleSlotAsync(id, dto);
            return StatusCode(response.StatusCode, response);
        }
    }
}
