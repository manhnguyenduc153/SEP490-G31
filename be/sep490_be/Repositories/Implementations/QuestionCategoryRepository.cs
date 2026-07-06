using sep490_be.Models;
using sep490_be.Repositories.Common;
using sep490_be.Repositories.Interfaces;

namespace sep490_be.Repositories.Implementations
{
    public class QuestionCategoryRepository : BaseRepository<QuestionCategory, ApplicationDbContext>, IQuestionCategoryRepository
    {
        public QuestionCategoryRepository(ApplicationDbContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
        {
        }
    }
}

