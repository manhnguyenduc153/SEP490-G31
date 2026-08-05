using sep490_be.Models;
using sep490_be.Repositories.Common;

namespace sep490_be.Repositories.Interfaces
{
    public interface IQuestionRepository : IBaseRepository<Question, ApplicationDbContext>
    {
        Task<bool> IsUsedInExamAsync(int questionId);

        // Real removal from DB (not IsDeleted = true): deletes QuestionAnswers then the Question.
        Task HardDeleteAsync(int questionId);

        // Real removal of specific answer rows (used when editing a question and some answers
        // are no longer present in the submitted list).
        Task RemoveAnswersAsync(IEnumerable<int> answerIds);
    }
}

