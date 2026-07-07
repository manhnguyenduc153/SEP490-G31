using sep490_be.Models;
using sep490_be.Repositories.Common;
using sep490_be.Repositories.Interfaces;

namespace sep490_be.Repositories.Implementations
{
    public class StudentRepository : BaseRepository<Student, ApplicationDbContext>, IStudentRepository
    {
        public StudentRepository(ApplicationDbContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
        {
        }
    }
}

