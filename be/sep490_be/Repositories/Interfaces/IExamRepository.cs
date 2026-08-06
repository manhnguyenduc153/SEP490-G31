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

        // Used by ValidateQuestionCourseMatchAsync in ExamService to keep exam-question course
        // validation out of the service's own DbContext access.
        Task<int?> GetClassCourseIdAsync(int classId);
        Task<bool> HasQuestionCourseMismatchAsync(int courseId, List<int> questionIds);

        // ExamQuestion is a join entity for Exam, so it isn't the generic T of this repository;
        // these wrap the raw DbSet access that CreateAsync/EditAsync/CopyAsync need.
        Task AddExamQuestionsAsync(IEnumerable<ExamQuestion> examQuestions);
        Task RemoveExamQuestionsByExamIdAsync(int examId);
        IQueryable<ExamQuestion> FindAllExamQuestions(bool trackChanges = false);

        // ExamAttempt/ExamAnswer are child entities of Exam (not the generic T of this repository)
        // used by the attempt/submission/grading flows — kept here rather than a separate repository
        // since they only ever make sense scoped to an Exam.
        IQueryable<ExamAttempt> FindAllAttempts(bool trackChanges = false);
        Task<ExamAttempt> AddAttemptAsync(ExamAttempt attempt);

        IQueryable<ExamAnswer> FindAllAnswers(bool trackChanges = false);
        Task AddAnswersAsync(IEnumerable<ExamAnswer> answers);
        Task RemoveAnswersByAttemptIdAsync(int attemptId);
    }
}

