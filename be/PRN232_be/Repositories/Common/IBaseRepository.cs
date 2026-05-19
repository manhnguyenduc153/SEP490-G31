using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace PRN232_be.Repositories.Common
{
    public interface IBaseRepository<T, TContext>
        where T : class
        where TContext : DbContext
    {
        Task<T?> GetByIdAsync<TKey>(TKey id) where TKey : notnull;
        Task<T?> GetByIdAsync(int id);
        Task<IEnumerable<T>> GetAllAsync();
        Task<T> AddAsync(T entity);
        Task<IEnumerable<T>> AddRangeAsync(IEnumerable<T> entities);
        Task UpdateAsync(T entity);
        Task DeleteAsync(T entity);
        Task UpdateRangeAsync(IEnumerable<T> entities);
        Task DeleteRangeAsync(IEnumerable<T> entities);
        
        IQueryable<T> FindAll(bool trackChanges = false);
        IQueryable<T> FindByCondition(Expression<Func<T, bool>> expression, bool trackChanges = false);
        Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate);
        
        Task<IEnumerable<TResult>> DapperQueryAsync<TResult>(string sql, object? param = null);
        Task<TResult?> DapperGetAsync<TResult>(string sql, object? param = null);
        Task<int> DapperExecuteAsync(string sql, object? param = null);
        Task<TResult?> DapperExecuteScalarAsync<TResult>(string sql, object? param = null);
        
        Task<int> SaveChangesAsync();
        Task<IDbContextTransaction> BeginTransactionAsync();
        Task CommitTransactionAsync();
        Task RollbackTransactionAsync();
    }
}
