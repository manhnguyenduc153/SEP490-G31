using Microsoft.EntityFrameworkCore;
using sep490_be.Models;
using sep490_be.Repositories.Common;
using sep490_be.Repositories.Interfaces;

namespace sep490_be.Repositories.Implementations
{
    public class QuestionRepository : BaseRepository<Question, ApplicationDbContext>, IQuestionRepository
    {
        public QuestionRepository(ApplicationDbContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
        {
        }

        public async Task<bool> IsUsedInExamAsync(int questionId)
        {
            return await _dbContext.ExamQuestions.AnyAsync(eq => eq.QuestionId == questionId)
                || await _dbContext.ExamAnswers.AnyAsync(ea => ea.QuestionId == questionId);
        }

        public async Task HardDeleteAsync(int questionId)
        {
            await BeginTransactionAsync();
            try
            {
                await _dbContext.QuestionAnswers.Where(qa => qa.QuestionId == questionId).ExecuteDeleteAsync();
                await _dbContext.Questions.Where(q => q.Id == questionId).ExecuteDeleteAsync();
                await CommitTransactionAsync();
            }
            catch
            {
                await RollbackTransactionAsync();
                throw;
            }
        }

        public async Task RemoveAnswersAsync(IEnumerable<int> answerIds)
        {
            var ids = answerIds.ToList();
            if (ids.Count == 0) return;

            await _dbContext.QuestionAnswers.Where(a => ids.Contains(a.Id)).ExecuteDeleteAsync();
        }
    }
}

