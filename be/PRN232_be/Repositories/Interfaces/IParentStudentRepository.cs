using PRN232_be.Models;
using PRN232_be.Repositories.Common;

namespace PRN232_be.Repositories.Interfaces
{
    public interface IParentStudentRepository : IBaseRepository<ParentStudent, ApplicationDbContext>
    {
        // Kế thừa toàn bộ CRUD từ IBaseRepository
    }
}
