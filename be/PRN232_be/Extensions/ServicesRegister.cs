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
            services.AddScoped<IProductRepository, ProductRepository>();
            services.AddScoped<IQuestionCategoryRepository, QuestionCategoryRepository>();
            services.AddScoped<ITeacherRepository, TeacherRepository>();
            services.AddScoped<IRoomRepository, RoomRepository>();
            services.AddScoped<ICourseRepository, CourseRepository>();
            
            // Services
            services.AddScoped<IProductService, ProductService>();
            services.AddScoped<IQuestionCategoryService, QuestionCategoryService>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<ITeacherService, TeacherService>();
            services.AddScoped<IFileService, FileService>();
            services.AddScoped<IRoomService, RoomService>();
            services.AddScoped<ICourseService, CourseService>();
        }
    }
}
