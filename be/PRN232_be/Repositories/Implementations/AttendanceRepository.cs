using PRN232_be.Models;
using PRN232_be.Repositories.Common;
using PRN232_be.Repositories.Interfaces;

namespace PRN232_be.Repositories.Implementations
{
    public class AttendanceRepository : BaseRepository<Models.Attendance, ApplicationDbContext>, IAttendanceRepository
    {
        public AttendanceRepository(ApplicationDbContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
        {
        }
    }
}
