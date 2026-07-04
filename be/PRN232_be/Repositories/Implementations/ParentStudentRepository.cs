using PRN232_be.Models;
using PRN232_be.Repositories.Common;
using PRN232_be.Repositories.Interfaces;

namespace PRN232_be.Repositories.Implementations
{
    public class ParentStudentRepository : BaseRepository<ParentStudent, ApplicationDbContext>, IParentStudentRepository
    {
        public ParentStudentRepository(ApplicationDbContext context, IUnitOfWork unitOfWork)
            : base(context, unitOfWork) { }
    }
}
