using sep490_be.Models;
using sep490_be.Repositories.Common;

namespace sep490_be.Repositories.Interfaces
{
    public interface IStudentRepository : IBaseRepository<Student, ApplicationDbContext>
    {
        // True if the student has enrollment/attendance/exam history that would violate
        // FK constraints (all Restrict) on a real hard delete.
        Task<bool> IsInUseAsync(int studentId);

        // Real removal from DB (not IsDeleted = true).
        Task HardDeleteAsync(int studentId);
    }
}

