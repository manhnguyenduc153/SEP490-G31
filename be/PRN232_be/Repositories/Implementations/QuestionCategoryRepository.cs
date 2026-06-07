using PRN232_be.Models;
using PRN232_be.Repositories.Common;
using PRN232_be.Repositories.Interfaces;

namespace PRN232_be.Repositories.Implementations
{
    public class QuestionCategoryRepository : BaseRepository<QuestionCategory, ApplicationDbContext>, IQuestionCategoryRepository
    {
        public QuestionCategoryRepository(ApplicationDbContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
        {
        }
    }
}
