using Microsoft.EntityFrameworkCore.Storage;
using PAFA.Domain.IRepository;
using PAFA.Domain.Repositories;
using PAFA.Infrastructure.Data;

namespace PAFA.Infrastructure.Repositories;

/// <summary>
/// Unit of Work pattern implementation coordinating multiple repositories.
/// Ensures atomic transactions and proper disposal of resources.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly PafaDbContext _context;
    private IDbContextTransaction? _transaction;

    // Lazy-initialized repositories
    private IIngestionJobRepository? _ingestionJobs;
    private IIngestionFileRepository? _ingestionFiles;
    private IShipperRepository? _shippers;
    private IReportRepository? _reports;

    public UnitOfWork(PafaDbContext context)
    {
        _context = context;
    }

    public IIngestionJobRepository IngestionJobs =>
        _ingestionJobs ??= new IngestionJobRepository(_context);

    public IIngestionFileRepository IngestionFiles =>
        _ingestionFiles ??= new IngestedFileRepository(_context);

    public IShipperRepository Shippers =>
        _shippers ??= new ShipperRepository(_context);

    public IReportRepository Reports =>
        _reports ??= new ReportRepository(_context);

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _context.SaveChangesAsync(ct);
    }

    public async Task BeginTransactionAsync(CancellationToken ct = default)
    {
        _transaction = await _context.Database.BeginTransactionAsync(ct);
    }

    public async Task CommitTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync(ct);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync(ct);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}
