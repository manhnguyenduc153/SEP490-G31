using sep490_be.Models;
using sep490_be.Repositories.Common;

namespace sep490_be.Repositories.Interfaces
{
    public interface ILearningMaterialRepository : IBaseRepository<LearningMaterial, ApplicationDbContext>
    {
        IQueryable<LearningMaterial> GetMaterialsWithDetails();
        Task<LearningMaterial?> GetMaterialWithDetailsByIdAsync(int id);
    }
}

