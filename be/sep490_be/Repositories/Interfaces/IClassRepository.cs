using sep490_be.Models;
using sep490_be.Repositories.Common;

namespace sep490_be.Repositories.Interfaces
{
    public interface IClassRepository : IBaseRepository<Class, ApplicationDbContext>
    {
        IQueryable<Class> GetClassesWithBasicDetails();
        Task<Class?> GetClassWithDetailsByIdAsync(int id);
        Task<Class?> GetClassWithBasicDetailsByIdAsync(int id);
        Task<Class?> GetClassForEditAsync(int id);
    }
}

