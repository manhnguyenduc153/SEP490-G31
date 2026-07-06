using sep490_be.Models;
using sep490_be.Repositories.Common;
using sep490_be.Repositories.Interfaces;

namespace sep490_be.Repositories.Implementations
{
    public class AttendanceRepository : BaseRepository<Models.Attendance, ApplicationDbContext>, IAttendanceRepository
    {
        public AttendanceRepository(ApplicationDbContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
        {
        }
    }
}

