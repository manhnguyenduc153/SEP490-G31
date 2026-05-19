using Microsoft.EntityFrameworkCore.Storage;
using System.Threading.Tasks;

namespace PRN232_be.Repositories.Common
{
    public interface IUnitOfWork
    {
        Task<int> SaveChangesAsync();
        Task<IDbContextTransaction> BeginTransactionAsync();
        Task CommitTransactionAsync();
        Task RollbackTransactionAsync();
    }
}
