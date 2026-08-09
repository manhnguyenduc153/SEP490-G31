using sep490_be.Models;
using sep490_be.Repositories.Interfaces;
using sep490_be.Repositories.Common;

namespace sep490_be.Repositories.Implementations
{
    public class StudentGradeOverrideRepository : BaseRepository<StudentGradeOverride, ApplicationDbContext>, IStudentGradeOverrideRepository
    {
        public StudentGradeOverrideRepository(ApplicationDbContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
        {
        }
    }
}
