using sep490_be.Models;
using sep490_be.Repositories.Common;

namespace sep490_be.Repositories.Interfaces
{
    public interface IQuestionCategoryRepository : IBaseRepository<QuestionCategory, ApplicationDbContext>
    {
        Task<bool> IsUsedInQuestionsAsync(int categoryId);

        // Real removal from DB (not IsDeleted = true).
        Task HardDeleteAsync(int categoryId);
    }
}

