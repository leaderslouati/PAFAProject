using Microsoft.EntityFrameworkCore;
using PAFA.Domain.Entities;
using PAFA.Domain.Repositories;
using PAFA.Infrastructure.Persistence;
using System.Linq.Expressions;

namespace PAFA.Infrastructure.Repositories;  

public class BaseRepository<T>(PafaDbContext ctx) : IBaseRepository<T>
 where T : BaseEntity
{
    protected readonly PafaDbContext _ctx = ctx;
    protected readonly DbSet<T> _set = ctx.Set<T>();

    public Task<T?> GetByIdAsync(object id, CancellationToken ct = default)
        => _set.FindAsync(new[] { id }, ct).AsTask();
    public Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
        => _set.ToListAsync(ct).ContinueWith(t => (IReadOnlyList<T>)t.Result, ct);
    public async Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> pred, CancellationToken ct = default)
        => await _set.Where(pred).ToListAsync(ct);
    public Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> pred, CancellationToken ct = default)
        => _set.FirstOrDefaultAsync(pred, ct);
    public Task<bool> ExistsAsync(Expression<Func<T, bool>> pred, CancellationToken ct = default)
        => _set.AnyAsync(pred, ct);
    public Task<int> CountAsync(Expression<Func<T, bool>>? pred = null, CancellationToken ct = default)
        => pred is null ? _set.CountAsync(ct) : _set.CountAsync(pred, ct);
    public async Task AddAsync(T entity, CancellationToken ct = default)
        => await _set.AddAsync(entity, ct);
    public async Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default)
        => await _set.AddRangeAsync(entities, ct);
    public void Update(T entity) => _set.Update(entity);
    public void Remove(T entity) => _set.Remove(entity);
    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _ctx.SaveChangesAsync(ct);
}