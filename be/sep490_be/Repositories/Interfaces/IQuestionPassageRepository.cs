using sep490_be.Models;
using sep490_be.Repositories.Common;

namespace sep490_be.Repositories.Interfaces
{
    public interface IQuestionPassageRepository : IBaseRepository<QuestionPassage, ApplicationDbContext>
    {
        // Questions that belong to this passage, identified purely by the PassageId FK.
        Task<List<int>> GetQuestionIdsAsync(int passageId);

        Task<bool> IsUsedInExamAsync(int passageId, List<int> questionIds);

        // Real removal from DB (not IsDeleted = true): deletes answers -> questions -> passage.
        Task HardDeleteAsync(int passageId, List<int> questionIds);
    }
}
