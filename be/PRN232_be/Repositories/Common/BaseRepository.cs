using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;
using System.Linq.Expressions;

namespace PRN232_be.Repositories.Common
{
    public class BaseRepository<T, TContext> : IBaseRepository<T, TContext>
        where T : class
        where TContext : DbContext
    {
        protected readonly TContext _dbContext;
        protected readonly IUnitOfWork _unitOfWork;

        public BaseRepository(TContext dbContext, IUnitOfWork unitOfWork)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        #region EF Core CRUD
        public async Task<T?> GetByIdAsync<TKey>(TKey id)
            where TKey : notnull
        {
            return await _dbContext.Set<T>().FindAsync(new object[] { id });
        }

        public Task<T?> GetByIdAsync(int id) => GetByIdAsync<int>(id);

        public async Task<IEnumerable<T>> GetAllAsync() =>
            await _dbContext.Set<T>().ToListAsync();

        public async Task<T> AddAsync(T entity)
        {
            await _dbContext.Set<T>().AddAsync(entity);
            return entity;
        }

        public async Task<IEnumerable<T>> AddRangeAsync(IEnumerable<T> entities)
        {
            await _dbContext.Set<T>().AddRangeAsync(entities);
            return entities;
        }
        public Task UpdateAsync(T entity)
        {
            _dbContext.Set<T>().Update(entity);
            return Task.CompletedTask;
        }
        public Task DeactiveAsync(T entity)
        {
            var isDeletedProp = typeof(T).GetProperty("IsDeleted");
            if (isDeletedProp != null && isDeletedProp.CanWrite)
            {
                isDeletedProp.SetValue(entity, true);
                
                var deletedAtProp = typeof(T).GetProperty("DeletedAt");
                if (deletedAtProp != null && deletedAtProp.CanWrite)
                {
                    deletedAtProp.SetValue(entity, DateTime.UtcNow);
                }
                
                _dbContext.Set<T>().Update(entity);
            }
            return Task.CompletedTask;
        }
        public Task DeleteAsync(T entity)
        {
            _dbContext.Set<T>().Remove(entity);
            return Task.CompletedTask;
        }
        public Task UpdateRangeAsync(IEnumerable<T> entities)
        {
            _dbContext.Set<T>().UpdateRange(entities);
            return Task.CompletedTask;
        }
        public Task DeleteRangeAsync(IEnumerable<T> entities)
        {
            _dbContext.Set<T>().RemoveRange(entities);
            return Task.CompletedTask;
        }

        #endregion

        #region EF Core Query
        public IQueryable<T> FindAll(bool trackChanges = false) =>
            trackChanges ? _dbContext.Set<T>() : _dbContext.Set<T>().AsNoTracking();

        public IQueryable<T> FindByCondition(Expression<Func<T, bool>> expression, bool trackChanges = false) =>
            trackChanges ? _dbContext.Set<T>().Where(expression)
                         : _dbContext.Set<T>().Where(expression).AsNoTracking();

        public async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbContext.Set<T>().AnyAsync(predicate);
        }
        #endregion

        #region Dapper Support
        protected IDbConnection Connection => _dbContext.Database.GetDbConnection();

        public async Task<IEnumerable<TResult>> DapperQueryAsync<TResult>(string sql, object? param = null) =>
            await Connection.QueryAsync<TResult>(sql, param);

        public async Task<TResult?> DapperGetAsync<TResult>(string sql, object? param = null) =>
            await Connection.QueryFirstOrDefaultAsync<TResult>(sql, param);

        public async Task<int> DapperExecuteAsync(string sql, object? param = null) =>
            await Connection.ExecuteAsync(sql, param);

        public async Task<TResult?> DapperExecuteScalarAsync<TResult>(string sql, object? param = null) =>
            await Connection.ExecuteScalarAsync<TResult>(sql, param);
        #endregion

        #region Transaction
        public Task<int> SaveChangesAsync() => _unitOfWork.SaveChangesAsync();

        public Task<IDbContextTransaction> BeginTransactionAsync() => _unitOfWork.BeginTransactionAsync();
        public Task CommitTransactionAsync() => _unitOfWork.CommitTransactionAsync();
        public Task RollbackTransactionAsync() => _unitOfWork.RollbackTransactionAsync();
        #endregion
    }
}
