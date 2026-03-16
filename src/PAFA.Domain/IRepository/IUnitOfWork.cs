using PAFA.Domain.IRepository;

namespace PAFA.Domain.Repositories;

public interface IUnitOfWork : IDisposable
{
    IIngestionJobRepository IngestionJobs { get; }
    IIngestionFileRepository IngestionFiles { get; }
    IShipperRepository Shippers { get; }
    IReportRepository Reports { get; }
    IMetricValueRepository MetricValues { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitTransactionAsync(CancellationToken ct = default);
    Task RollbackTransactionAsync(CancellationToken ct = default);
}