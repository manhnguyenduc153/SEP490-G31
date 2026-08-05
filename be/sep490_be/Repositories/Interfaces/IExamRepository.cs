using sep490_be.Models;
using sep490_be.Repositories.Common;

namespace sep490_be.Repositories.Interfaces
{
    public interface IExamRepository : IBaseRepository<Exam, ApplicationDbContext>
    {
        Task<bool> HasAttemptsAsync(int examId);

        // ExamStudent -> ExamSchedule is a Restrict FK: deleting an ExamSchedule that still has
        // assigned students would throw a raw DB constraint error. Block deletion instead.
        Task<bool> HasExamStudentsAsync(int examId);

        // Real removal from DB (not IsDeleted = true): student grades -> schedules -> exam questions -> exam.
        Task HardDeleteAsync(int examId);
    }
}

