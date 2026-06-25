using PRN232_be.Models;
using PRN232_be.Repositories.Common;
using PRN232_be.Repositories.Interfaces;

namespace PRN232_be.Repositories.Implementations
{
    public class CourseRepository : BaseRepository<Course, ApplicationDbContext>, ICourseRepository
    {
        public CourseRepository(ApplicationDbContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
        {
        }
    }
}
