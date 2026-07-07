using sep490_be.Models;
using sep490_be.Repositories.Common;
using sep490_be.Repositories.Interfaces;

namespace sep490_be.Repositories.Implementations
{
    public class ExamRepository : BaseRepository<Exam, ApplicationDbContext>, IExamRepository
    {
        public ExamRepository(ApplicationDbContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
        {
        }
    }
}

