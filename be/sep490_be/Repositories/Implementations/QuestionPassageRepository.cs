using Microsoft.EntityFrameworkCore;
using sep490_be.Models;
using sep490_be.Repositories.Common;
using sep490_be.Repositories.Interfaces;

namespace sep490_be.Repositories.Implementations
{
    public class QuestionPassageRepository : BaseRepository<QuestionPassage, ApplicationDbContext>, IQuestionPassageRepository
    {
        public QuestionPassageRepository(ApplicationDbContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
        {
        }

        public async Task<List<int>> GetQuestionIdsAsync(int passageId)
        {
            return await _dbContext.Questions
                .Where(q => q.PassageId == passageId)
                .Select(q => q.Id)
                .ToListAsync();
        }

        public async Task<bool> IsUsedInExamAsync(int passageId, List<int> questionIds)
        {
            var usedAsPassage = await _dbContext.ExamQuestions
                .AnyAsync(eq => eq.PassageId.HasValue && eq.PassageId.Value == passageId || questionIds.Contains(eq.QuestionId));
            if (usedAsPassage) return true;

            return await _dbContext.ExamAnswers.AnyAsync(ea => questionIds.Contains(ea.QuestionId));
        }

        public async Task HardDeleteAsync(int passageId, List<int> questionIds)
        {
            await BeginTransactionAsync();
            try
            {
                if (questionIds.Count > 0)
                {
                    await _dbContext.QuestionAnswers.Where(qa => qa.QuestionId.HasValue && questionIds.Contains(qa.QuestionId.Value)).ExecuteDeleteAsync();
                    await _dbContext.Questions.Where(q => questionIds.Contains(q.Id)).ExecuteDeleteAsync();
                }

                await _dbContext.QuestionPassages.Where(p => p.Id == passageId).ExecuteDeleteAsync();
                await CommitTransactionAsync();
            }
            catch
            {
                await RollbackTransactionAsync();
                throw;
            }
        }
    }
}
