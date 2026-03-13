using PAFA.Domain.IRepository;

namespace PAFA.Domain.Repositories;

/// <summary>
/// Unit of Work pattern implementation for atomic transactions across repositories.
/// Ensures consistency and proper transaction management.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    // Repository accessors
    IIngestionJobRepository IngestionJobs { get; }
    IIngestionFileRepository IngestionFiles { get; }
    IShipperRepository Shippers { get; }
    IReportRepository Reports { get; }
    
    /// <summary>
    /// Save all pending changes to the database.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Begin a new database transaction.
    /// </summary>
    Task BeginTransactionAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Commit the current transaction.
    /// </summary>
    Task CommitTransactionAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Rollback the current transaction.
    /// </summary>
    Task RollbackTransactionAsync(CancellationToken ct = default);
}
