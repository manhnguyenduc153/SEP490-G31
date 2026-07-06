using Microsoft.EntityFrameworkCore.Storage;
using System.Threading.Tasks;

namespace sep490_be.Repositories.Common
{
    public interface IUnitOfWork
    {
        Task<int> SaveChangesAsync();
        Task<IDbContextTransaction> BeginTransactionAsync();
        Task CommitTransactionAsync();
        Task RollbackTransactionAsync();
    }
}

