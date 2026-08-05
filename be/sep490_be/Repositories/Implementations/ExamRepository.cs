using Microsoft.EntityFrameworkCore;
using sep490_be.Models;
using sep490_be.Repositories.Common;
using sep490_be.Repositories.Interfaces;

namespace sep490_be.Repositories.Implementations
{
    public class ExamRepository : BaseRepository<Exam, ApplicationDbContext>, IExamRepository
    {
        public ExamRepository(ApplicationDbContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
        {
        }

        public async Task<bool> HasAttemptsAsync(int examId)
        {
            return await _dbContext.ExamAttempts.AnyAsync(a => a.ExamId == examId);
        }

        public async Task<bool> HasExamStudentsAsync(int examId)
        {
            return await _dbContext.ExamStudents.AnyAsync(es => es.ExamSchedule.ExamId == examId);
        }

        public async Task HardDeleteAsync(int examId)
        {
            await BeginTransactionAsync();
            try
            {
                await _dbContext.StudentGrades.Where(g => g.ExamId == examId).ExecuteDeleteAsync();
                await _dbContext.ExamSchedules.Where(s => s.ExamId == examId).ExecuteDeleteAsync();
                await _dbContext.ExamQuestions.Where(q => q.ExamId == examId).ExecuteDeleteAsync();
                await _dbContext.Exams.Where(e => e.Id == examId).ExecuteDeleteAsync();
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

