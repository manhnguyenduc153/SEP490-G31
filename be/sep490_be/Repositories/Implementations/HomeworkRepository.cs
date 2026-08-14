using System;
using sep490_be.Models;
using sep490_be.Repositories.Interfaces;
using sep490_be.Repositories.Common;

using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using sep490_be.Enums;

namespace sep490_be.Repositories.Implementations
{
    public class HomeworkRepository : BaseRepository<Homework, ApplicationDbContext>, IHomeworkRepository
    {
        public HomeworkRepository(ApplicationDbContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
        {
        }

        public async Task<bool> ClassExistsAsync(int classId)
        {
            return await _dbContext.Classes.AsNoTracking().AnyAsync(c => c.Id == classId && !c.IsDeleted);
        }

        public async Task<bool> IsClassOpenForHomeworkAsync(int classId)
        {
            var today = DateTime.UtcNow.Date;
            return await _dbContext.Classes.AsNoTracking().AnyAsync(c =>
                c.Id == classId && !c.IsDeleted &&
                c.Status == (int)ClassStatus.Active &&
                (!c.StartDate.HasValue || c.StartDate.Value.Date <= today));
        }

        public async Task<Student?> ResolveStudentByUsernameAsync(string username)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserName == username || u.Id == username);
            var email = user?.Email ?? username;

            return await _dbContext.Students.FirstOrDefaultAsync(s =>
                (s.Email != null && s.Email.ToLower() == email.ToLower()) ||
                (s.Code != null && s.Code.ToLower() == username.ToLower()) ||
                (s.Email != null && s.Email.ToLower() == username.ToLower()));
        }

        public async Task<bool> IsStudentEnrolledInClassAsync(int studentId, int classId)
        {
            return await _dbContext.StudentClasses.AnyAsync(sc => sc.StudentId == studentId && sc.ClassId == classId);
        }

        public async Task<bool> IsStudentEnrolledInClassWithStatusAsync(int studentId, int classId, int[] statuses)
        {
            return await _dbContext.StudentClasses.AnyAsync(sc => sc.StudentId == studentId && sc.ClassId == classId && statuses.Contains(sc.Status));
        }

        public async Task<bool> TeacherExistsAsync(int teacherId)
        {
            return await _dbContext.Teachers.AsNoTracking().AnyAsync(t => t.Id == teacherId && !t.IsDeleted);
        }

        public async Task<bool> IsTeacherAssignedToClassAsync(int teacherId, int classId)
        {
            var classEntity = await _dbContext.Classes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == classId && !c.IsDeleted);
            return classEntity != null && classEntity.TeacherId == teacherId;
        }
    }
}

