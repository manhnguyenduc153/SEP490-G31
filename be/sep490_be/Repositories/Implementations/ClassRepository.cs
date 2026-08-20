using Microsoft.EntityFrameworkCore;
using sep490_be.Models;
using sep490_be.Repositories.Common;
using sep490_be.Repositories.Interfaces;

namespace sep490_be.Repositories.Implementations
{
    public class ClassRepository : BaseRepository<Class, ApplicationDbContext>, IClassRepository
    {
        public ClassRepository(ApplicationDbContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
        {
        }

        public IQueryable<Class> GetClassesWithBasicDetails()
        {
            return FindAll()
                .Include(c => c.Course)
                .Include(c => c.Teacher)
                .Include(c => c.Semester)
                .Include(c => c.StudentClasses)
                .Where(c => !c.IsDeleted)
                .AsQueryable();
        }

        public async Task<Class?> GetClassWithDetailsByIdAsync(int id)
        {
            return await FindAll()
                .Include(c => c.Course)
                .Include(c => c.Teacher)
                .Include(c => c.Semester)
                .Include(c => c.StudentClasses)
                    .ThenInclude(sc => sc.Student)
                .Include(c => c.ClassSchedules)
                    .ThenInclude(cs => cs.TimeSlot)
                .Include(c => c.ClassSchedules)
                    .ThenInclude(cs => cs.Room)
                .Include(c => c.ClassSchedules)
                    .ThenInclude(cs => cs.Teacher)
                .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
        }

        public async Task<Class?> GetClassWithBasicDetailsByIdAsync(int id)
        {
            return await FindAll()
                .Include(c => c.Course)
                .Include(c => c.Teacher)
                .Include(c => c.Semester)
                .Include(c => c.StudentClasses)
                .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
        }

        public async Task<Class?> GetClassForEditAsync(int id)
        {
            return await FindAll(trackChanges: true)
                .Include(c => c.StudentClasses)
                .Include(c => c.ClassSchedules)
                .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
        }
    }
}

