using Microsoft.EntityFrameworkCore;
using sep490_be.Models;
using sep490_be.Repositories.Common;
using sep490_be.Repositories.Interfaces;

namespace sep490_be.Repositories.Implementations
{
    public class StudentRegistrationRepository : BaseRepository<StudentRegistration, ApplicationDbContext>, IStudentRegistrationRepository
    {
        public StudentRegistrationRepository(ApplicationDbContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
        {
        }

        public IQueryable<StudentRegistration> GetRegistrationsWithDetails()
        {
            return FindAll()
                .Include(sr => sr.Student)
                .Include(sr => sr.Course)
                .Include(sr => sr.Semester)
                .AsQueryable();
        }

        public async Task<StudentRegistration?> GetRegistrationWithDetailsByIdAsync(int id)
        {
            return await FindAll()
                .Include(sr => sr.Student)
                .Include(sr => sr.Course)
                .Include(sr => sr.Semester)
                .FirstOrDefaultAsync(sr => sr.Id == id);
        }

        public async Task<StudentRegistration?> GetRegistrationByStudentCourseSemesterAsync(int studentId, int courseId, int semesterId)
        {
            return await FindAll()
                .FirstOrDefaultAsync(sr => sr.SemesterId == semesterId 
                                        && sr.StudentId == studentId 
                                        && sr.CourseId == courseId);
        }
    }
}
