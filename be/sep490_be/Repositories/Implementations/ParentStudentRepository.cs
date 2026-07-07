using sep490_be.Models;
using sep490_be.Repositories.Common;
using sep490_be.Repositories.Interfaces;

namespace sep490_be.Repositories.Implementations
{
    public class ParentStudentRepository : BaseRepository<ParentStudent, ApplicationDbContext>, IParentStudentRepository
    {
        public ParentStudentRepository(ApplicationDbContext context, IUnitOfWork unitOfWork)
            : base(context, unitOfWork) { }
    }
}

