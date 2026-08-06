using sep490_be.Models;
using sep490_be.Repositories.Common;

namespace sep490_be.Repositories.Interfaces
{
    public interface IStudentRegistrationRepository : IBaseRepository<StudentRegistration, ApplicationDbContext>
    {
        IQueryable<StudentRegistration> GetRegistrationsWithDetails();
        Task<StudentRegistration?> GetRegistrationWithDetailsByIdAsync(int id);
        Task<StudentRegistration?> GetRegistrationByStudentCourseSemesterAsync(int studentId, int courseId, int semesterId);
    }
}
