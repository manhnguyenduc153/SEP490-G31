using PRN232_be.Repositories.Interfaces;
using PRN232_be.Repositories.Implementations;
using PRN232_be.Services.Interfaces;
using PRN232_be.Services.Implementations;
using PRN232_be.Repositories.Common;
using PRN232_be.Models;

namespace PRN232_be.Extensions
{
    public static class ServicesRegister
    {
        public static void RegisterCustomServices(this IServiceCollection services)
        {
            // Repositories & UoW
            services.AddScoped<IUnitOfWork, UnitOfWork<ApplicationDbContext>>();
            services.AddScoped(typeof(IBaseRepository<,>), typeof(BaseRepository<,>));
            services.AddScoped<IExamRepository, ExamRepository>();
            services.AddScoped<IProductRepository, ProductRepository>();
            services.AddScoped<IQuestionCategoryRepository, QuestionCategoryRepository>();
            services.AddScoped<IAttendanceRepository, AttendanceRepository>();
            services.AddScoped<IQuestionRepository, QuestionRepository>();
            services.AddScoped<ITeacherRepository, TeacherRepository>();
            services.AddScoped<IRoomRepository, RoomRepository>();
            services.AddScoped<ICourseRepository, CourseRepository>();
            services.AddScoped<IClassRepository, ClassRepository>();
            services.AddScoped<IStudentRepository, StudentRepository>();
            services.AddScoped<IParentStudentRepository, ParentStudentRepository>();
            services.AddScoped<IRoomRepository, RoomRepository>();
            services.AddScoped<IStudentRepository, StudentRepository>();
            services.AddScoped<IHomeworkRepository, HomeworkRepository>();
            services.AddScoped<IHomeworkSubmissionRepository, HomeworkSubmissionRepository>();
            services.AddScoped<ILearningMaterialRepository, LearningMaterialRepository>();
            
            // Services
            services.AddScoped<IExamService, ExamService>();
            services.AddScoped<IProductService, ProductService>();
            services.AddScoped<IQuestionCategoryService, QuestionCategoryService>();
            services.AddScoped<IAttendanceService, AttendanceService>();
            services.AddScoped<IQuestionService, QuestionService>();
            services.AddScoped<ICourseService, CourseService>();
            services.AddScoped<IStudentService, StudentService>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<ITeacherService, TeacherService>();
            services.AddScoped<IFileService, FileService>();
            services.AddScoped<IRoomService, RoomService>();
            services.AddScoped<ICourseService, CourseService>();
            services.AddScoped<IClassService, ClassService>();
            services.AddScoped<IStudentService, StudentService>();
            services.AddScoped<IRoomService, RoomService>();
            services.AddScoped<IParentStudentService, ParentStudentService>();
            services.AddScoped<IHomeworkService, HomeworkService>();
            services.AddScoped<IScheduleOptimizationService, ScheduleOptimizationService>();
            services.AddScoped<ILearningMaterialService, LearningMaterialService>();
            services.AddScoped<ISemesterService, SemesterService>();
        }
    }
}
