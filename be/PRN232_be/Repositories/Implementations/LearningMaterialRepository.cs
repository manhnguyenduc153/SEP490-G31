using PRN232_be.Models;
using PRN232_be.Repositories.Common;
using PRN232_be.Repositories.Interfaces;

namespace PRN232_be.Repositories.Implementations
{
    public class LearningMaterialRepository : BaseRepository<LearningMaterial, ApplicationDbContext>, ILearningMaterialRepository
    {
        public LearningMaterialRepository(ApplicationDbContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
        {
        }
    }
}
