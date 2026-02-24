// Infrastructure/Interfaces/IGenericRepository.cs
using System.Linq.Expressions;

namespace Infrastructure.Interfaces
{
    public interface IGenericRepository<T> where T : class
    {
        Task<T?> GetByIdAsync(int id);
        Task<IEnumerable<T>> GetAllAsync();

        // Remove ambiguous overload or make them distinct
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, string[] includes);
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken);
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, string[] includes, CancellationToken cancellationToken);

        Task AddAsync(T entity);
        Task AddAsync(T entity, CancellationToken cancellationToken);

        void Update(T entity);
        void Delete(T entity);

        Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate);
        Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null);

        // Fix ambiguous method
        Task<T> FindOneAsync(Expression<Func<T, bool>> criteria, string[] includes = null);
        Task<T> FindOneAsync(Expression<Func<T, bool>> criteria, string[] includes, CancellationToken cancellationToken);
    }
}