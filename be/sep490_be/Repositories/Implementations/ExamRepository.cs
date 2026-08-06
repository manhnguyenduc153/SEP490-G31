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

        public async Task<int?> GetClassCourseIdAsync(int classId)
        {
            var targetClass = await _dbContext.Classes
                .FirstOrDefaultAsync(c => c.Id == classId && !c.IsDeleted);
            return targetClass?.CourseId;
        }

        public async Task<bool> HasQuestionCourseMismatchAsync(int courseId, List<int> questionIds)
        {
            return await _dbContext.Questions
                .Include(q => q.QuestionCategory)
                .Include(q => q.QuestionPassage)
                    .ThenInclude(p => p.QuestionCategory)
                .Where(q => questionIds.Contains(q.Id))
                .AnyAsync(q =>
                    (q.QuestionCategory != null && q.QuestionCategory.CourseId.HasValue && q.QuestionCategory.CourseId.Value != courseId) ||
                    (q.QuestionPassage != null && q.QuestionPassage.QuestionCategory != null && q.QuestionPassage.QuestionCategory.CourseId.HasValue && q.QuestionPassage.QuestionCategory.CourseId.Value != courseId)
                );
        }

        public async Task AddExamQuestionsAsync(IEnumerable<ExamQuestion> examQuestions)
        {
            await _dbContext.ExamQuestions.AddRangeAsync(examQuestions);
        }

        public async Task RemoveExamQuestionsByExamIdAsync(int examId)
        {
            var existing = await _dbContext.ExamQuestions.Where(eq => eq.ExamId == examId).ToListAsync();
            _dbContext.ExamQuestions.RemoveRange(existing);
        }

        public IQueryable<ExamQuestion> FindAllExamQuestions(bool trackChanges = false) =>
            trackChanges ? _dbContext.ExamQuestions : _dbContext.ExamQuestions.AsNoTracking();

        public IQueryable<ExamAttempt> FindAllAttempts(bool trackChanges = false) =>
            trackChanges ? _dbContext.ExamAttempts : _dbContext.ExamAttempts.AsNoTracking();

        public async Task<ExamAttempt> AddAttemptAsync(ExamAttempt attempt)
        {
            await _dbContext.ExamAttempts.AddAsync(attempt);
            return attempt;
        }

        public IQueryable<ExamAnswer> FindAllAnswers(bool trackChanges = false) =>
            trackChanges ? _dbContext.ExamAnswers : _dbContext.ExamAnswers.AsNoTracking();

        public async Task AddAnswersAsync(IEnumerable<ExamAnswer> answers)
        {
            await _dbContext.ExamAnswers.AddRangeAsync(answers);
        }

        public async Task RemoveAnswersByAttemptIdAsync(int attemptId)
        {
            var existing = await _dbContext.ExamAnswers.Where(ea => ea.ExamAttemptId == attemptId).ToListAsync();
            _dbContext.ExamAnswers.RemoveRange(existing);
        }
    }
}

