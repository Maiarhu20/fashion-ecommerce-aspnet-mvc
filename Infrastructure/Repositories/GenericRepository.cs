// Infrastructure/Repositories/GenericRepository.cs
using Infrastructure.Data;
using Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Infrastructure.Repositories
{
    public class GenericRepository<T> : IGenericRepository<T> where T : class
    {
        protected readonly AppDbContext _context;
        private readonly DbSet<T> _dbSet;

        public GenericRepository(AppDbContext context)
        {
            _context = context;
            _dbSet = _context.Set<T>();
        }

        public async Task<T?> GetByIdAsync(int id)
        {
            return await _dbSet.FindAsync(id);
        }

        public async Task<IEnumerable<T>> GetAllAsync()
        {
            return await _dbSet.ToListAsync();
        }

        // No includes, no cancellation token
        public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.Where(predicate).ToListAsync();
        }

        // With includes, no cancellation token
        public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, string[] includes)
        {
            IQueryable<T> query = _dbSet;

            if (includes != null)
            {
                foreach (var include in includes)
                {
                    query = query.Include(include);
                }
            }

            return await query.Where(predicate).ToListAsync();
        }

        // No includes, with cancellation token
        public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken)
        {
            return await _dbSet.Where(predicate).ToListAsync(cancellationToken);
        }

        // With includes, with cancellation token
        public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, string[] includes, CancellationToken cancellationToken)
        {
            IQueryable<T> query = _dbSet;

            if (includes != null)
            {
                foreach (var include in includes)
                {
                    query = query.Include(include);
                }
            }

            return await query.Where(predicate).ToListAsync(cancellationToken);
        }

        public async Task AddAsync(T entity)
        {
            await _dbSet.AddAsync(entity);
        }

        public async Task AddAsync(T entity, CancellationToken cancellationToken)
        {
            await _dbSet.AddAsync(entity, cancellationToken);
        }

        public void Update(T entity)
        {
            _dbSet.Update(entity);
        }

        public void Delete(T entity)
        {
            _dbSet.Remove(entity);
        }

        public async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.AnyAsync(predicate);
        }

        public async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null)
        {
            if (predicate == null)
            {
                return await _dbSet.CountAsync();
            }
            return await _dbSet.Where(predicate).CountAsync();
        }

        // FindOneAsync without cancellation token
        public async Task<T> FindOneAsync(Expression<Func<T, bool>> criteria, string[] includes = null)
        {
            IQueryable<T> query = _dbSet;

            if (includes != null)
            {
                foreach (var include in includes)
                {
                    query = query.Include(include);
                }
            }

            return await query.FirstOrDefaultAsync(criteria);
        }

        // FindOneAsync with cancellation token (different signature)
        public async Task<T> FindOneAsync(Expression<Func<T, bool>> criteria, string[] includes, CancellationToken cancellationToken)
        {
            IQueryable<T> query = _dbSet;

            if (includes != null)
            {
                foreach (var include in includes)
                {
                    query = query.Include(include);
                }
            }

            return await query.FirstOrDefaultAsync(criteria, cancellationToken);
        }
    }
}