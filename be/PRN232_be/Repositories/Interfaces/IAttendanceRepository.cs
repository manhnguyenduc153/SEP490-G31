using PRN232_be.Models;
using PRN232_be.Repositories.Common;

namespace PRN232_be.Repositories.Interfaces
{
    public interface IAttendanceRepository : IBaseRepository<Models.Attendance, ApplicationDbContext>
    {
    }
}
