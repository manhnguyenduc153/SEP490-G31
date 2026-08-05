using Microsoft.EntityFrameworkCore;
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

        public async Task<bool> IsUsedInQuestionsAsync(int categoryId)
        {
            return await _dbContext.Questions.AnyAsync(q => q.CategoryId == categoryId);
        }

        public async Task HardDeleteAsync(int categoryId)
        {
            await _dbContext.QuestionCategories.Where(c => c.Id == categoryId).ExecuteDeleteAsync();
        }
    }
}

