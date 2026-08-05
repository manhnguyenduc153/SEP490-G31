using Microsoft.EntityFrameworkCore;
using sep490_be.Models;
using sep490_be.Repositories.Common;
using sep490_be.Repositories.Interfaces;

namespace sep490_be.Repositories.Implementations
{
    public class StudentRepository : BaseRepository<Student, ApplicationDbContext>, IStudentRepository
    {
        public StudentRepository(ApplicationDbContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
        {
        }

        public async Task<bool> IsInUseAsync(int studentId)
        {
            return await _dbContext.StudentClasses.AnyAsync(sc => sc.StudentId == studentId)
                || await _dbContext.Attendances.AnyAsync(a => a.StudentId == studentId)
                || await _dbContext.ExamAttempts.AnyAsync(a => a.StudentId == studentId)
                || await _dbContext.ExamStudents.AnyAsync(es => es.StudentId == studentId)
                || await _dbContext.ParentStudentLinks.AnyAsync(l => l.StudentId == studentId);
        }

        public async Task HardDeleteAsync(int studentId)
        {
            await _dbContext.Students.Where(s => s.Id == studentId).ExecuteDeleteAsync();
        }
    }
}

