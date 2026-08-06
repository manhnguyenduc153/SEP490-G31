using Microsoft.EntityFrameworkCore;
using sep490_be.Models;
using sep490_be.Repositories.Common;
using sep490_be.Repositories.Interfaces;

namespace sep490_be.Repositories.Implementations
{
    public class LearningMaterialRepository : BaseRepository<LearningMaterial, ApplicationDbContext>, ILearningMaterialRepository
    {
        public LearningMaterialRepository(ApplicationDbContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
        {
        }

        public IQueryable<LearningMaterial> GetMaterialsWithDetails()
        {
            return FindAll()
                .Include(x => x.Class)
                .Include(x => x.Course)
                .Include(x => x.ClassSchedule)
                .Include(x => x.Teacher)
                .AsQueryable();
        }

        public async Task<LearningMaterial?> GetMaterialWithDetailsByIdAsync(int id)
        {
            return await FindAll()
                .Include(x => x.Class)
                .Include(x => x.Course)
                .Include(x => x.ClassSchedule)
                .Include(x => x.Teacher)
                .FirstOrDefaultAsync(x => x.Id == id);
        }
    }
}

