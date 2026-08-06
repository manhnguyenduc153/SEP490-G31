using sep490_be.Repositories.Interfaces;
using sep490_be.Repositories.Implementations;
using sep490_be.Services.Interfaces;
using sep490_be.Services.Implementations;
using sep490_be.Repositories.Common;
using sep490_be.Models;

namespace sep490_be.Extensions
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
            services.AddScoped<IQuestionPassageRepository, QuestionPassageRepository>();
            services.AddScoped<ITeacherRepository, TeacherRepository>();
            services.AddScoped<IRoomRepository, RoomRepository>();
            services.AddScoped<ICourseRepository, CourseRepository>();
            services.AddScoped<IClassRepository, ClassRepository>();
            services.AddScoped<IStudentRepository, StudentRepository>();
            services.AddScoped<IParentStudentRepository, ParentStudentRepository>();
            services.AddScoped<IHomeworkRepository, HomeworkRepository>();
            services.AddScoped<IHomeworkSubmissionRepository, HomeworkSubmissionRepository>();
            services.AddScoped<ILearningMaterialRepository, LearningMaterialRepository>();
            services.AddScoped<IStudentRegistrationRepository, StudentRegistrationRepository>();
            services.AddScoped<ISemesterRepository, SemesterRepository>();
            
            // Services
            services.AddScoped<IExamService, ExamService>();
            services.AddScoped<IProductService, ProductService>();
            services.AddScoped<IQuestionCategoryService, QuestionCategoryService>();
            services.AddScoped<IAttendanceService, AttendanceService>();
            services.AddScoped<IQuestionService, QuestionService>();
            services.AddScoped<IQuestionPassageService, QuestionPassageService>();
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
            services.AddScoped<IDashboardService, DashboardService>();
            services.AddScoped<IStudentGradeService, StudentGradeService>();
            services.AddScoped<IReportService, ReportService>();
            services.AddScoped<INotificationService, NotificationService>();
        }
    }
}

