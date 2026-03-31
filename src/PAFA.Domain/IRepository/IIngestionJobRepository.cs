using PAFA.Domain.Entities;
using PAFA.Domain.Repositories;

namespace PAFA.Domain.IRepository;

/// <summary>
/// Repository for IngestionJob entity with domain-specific queries.
/// </summary>
public interface IIngestionJobRepository : IBaseRepository<IngestionJob>
{
    /// <summary>
    /// Get ingestion job by period with option to include files.
    /// </summary>
    Task<IngestionJob?> GetByPeriodAsync(int year, int month, CancellationToken ct = default);
    
    /// <summary>
    /// Get ingestion job with related files eagerly loaded.
    /// </summary>
    Task<IngestionJob?> GetWithFilesAsync(Guid jobId, CancellationToken ct = default);
    
    /// <summary>
    /// Get all active metric definitions for calculations.
    /// </summary>
    Task<IReadOnlyList<MetricDefinition>> GetMetricDefinitionsAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns the most recent IngestionJob for the given period regardless of status.
    /// Used during manual reprocess to locate the parent job and compute RetryCount.
    /// </summary>
    Task<IngestionJob?> GetLatestByPeriodAsync(int year, int month, CancellationToken ct = default);
}

/// <summary>
/// Temporary placeholder for MetricDefinition entity.
/// TODO: Implement full MetricDefinition entity in Phase 2.
/// </summary>
public record MetricDefinition(string MetricKey, string MetricName, string Category);
