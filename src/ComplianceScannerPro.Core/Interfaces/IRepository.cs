using System.Linq.Expressions;

namespace ComplianceScannerPro.Core.Interfaces;

public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id);
    Task<T?> GetAsync(Expression<Func<T, bool>> predicate);
    Task<T?> GetFirstOrDefaultAsync(Expression<Func<T, bool>> predicate);
    Task<List<T>> GetAllAsync();
    Task<List<T>> GetAllAsync(Expression<Func<T, bool>> predicate);
    Task<T> AddAsync(T entity);
    void Add(T entity);
    Task UpdateAsync(T entity);
    void Update(T entity);
    Task DeleteAsync(T entity);
    void Delete(T entity);
    Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null);
}