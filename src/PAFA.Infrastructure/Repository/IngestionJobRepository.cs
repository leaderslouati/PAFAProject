using Microsoft.EntityFrameworkCore;
using PAFA.Domain.Entities.ETL;
using PAFA.Domain.IRepository;
using PAFA.Domain.Repositories;
using PAFA.Infrastructure.Data;

namespace PAFA.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for IngestionJob entity.
/// </summary>
public class IngestionJobRepository : BaseRepository<IngestionJob>, IIngestionJobRepository
{
    public IngestionJobRepository(PafaDbContext dbContext) : base(dbContext) { }

    public async Task<IngestionJob?> GetByPeriodAsync(int year, int month, CancellationToken ct = default)
    {
        return await _dbContext.IngestionJobs
            .FirstOrDefaultAsync(j => j.PeriodYear == year && j.PeriodMonth == month, ct);
    }

    public async Task<IngestionJob?> GetWithFilesAsync(Guid jobId, CancellationToken ct = default)
    {
        return await _dbContext.IngestionJobs
            .Include(j => j.Files)
            .FirstOrDefaultAsync(j => j.Id == jobId, ct);
    }

    public async Task<IReadOnlyList<MetricDefinition>> GetMetricDefinitionsAsync(CancellationToken ct = default)
    {
        // TODO: Replace with actual MetricDefinition entity in Phase 2
        // For now, return hardcoded definitions
        await Task.CompletedTask;
        
        return new List<MetricDefinition>
        {
            new("READ_PERFORMANCE_PC1", "PC1 Read Performance %", "Performance"),
            new("READ_PERFORMANCE_PC2", "PC2 Read Performance %", "Performance"),
            new("READ_PERFORMANCE_PC3", "PC3 Read Performance %", "Performance"),
            new("READ_PERFORMANCE_PC4", "PC4 Read Performance %", "Performance"),
            new("AQ_AT_RISK", "AQ at Risk (MWH)", "Risk"),
            new("SP_COUNT_PC1", "Supply Points PC1", "Portfolio"),
            new("SP_COUNT_PC2", "Supply Points PC2", "Portfolio"),
            new("SP_COUNT_PC3", "Supply Points PC3", "Portfolio"),
            new("SP_COUNT_PC4", "Supply Points PC4", "Portfolio")
        };
    }
}
