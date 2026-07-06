using sep490_be.Models;
using sep490_be.Repositories.Common;

namespace sep490_be.Repositories.Interfaces
{
    public interface IQuestionCategoryRepository : IBaseRepository<QuestionCategory, ApplicationDbContext>
    {
    }
}

