using PAFA.Domain.IRepository;
using PAFA.Domain.Repositories;
using PAFA.Infrastructure.Persistence;
using PAFA.Infrastructure.Repository;

namespace PAFA.Infrastructure.Repositories;

/// <summary>
/// Unit of Work pattern implementation coordinating multiple repositories.
/// Ensures atomic transactions and proper disposal of resources.
/// </summary>
public class UnitOfWork(PafaDbContext ctx) : IUnitOfWork
{
    private Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? _tx;

    public IIngestionJobRepository IngestionJobs { get; } = new IngestionJobRepository(ctx);
    public IIngestionFileRepository IngestionFiles { get; } = new IngestionFileRepository(ctx);
    public IShipperRepository Shippers { get; } = new ShipperRepository(ctx);
    public IReportRepository Reports { get; } = new ReportRepository(ctx);
    public IMetricValueRepository MetricValues { get; } = new MetricValueRepository(ctx);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => ctx.SaveChangesAsync(ct);
    public async Task BeginTransactionAsync(CancellationToken ct = default)
        => _tx = await ctx.Database.BeginTransactionAsync(ct);
    public async Task CommitTransactionAsync(CancellationToken ct = default)
    { if (_tx != null) await _tx.CommitAsync(ct); }
    public async Task RollbackTransactionAsync(CancellationToken ct = default)
    { if (_tx != null) await _tx.RollbackAsync(ct); }
    public void Dispose() => _tx?.Dispose();
}
