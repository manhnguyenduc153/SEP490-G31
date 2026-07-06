using sep490_be.Models;
using sep490_be.Repositories.Common;

namespace sep490_be.Repositories.Interfaces
{
    public interface IParentStudentRepository : IBaseRepository<ParentStudent, ApplicationDbContext>
    {
        // Kế thừa toàn bộ CRUD từ IBaseRepository
    }
}

