using Microsoft.EntityFrameworkCore;
using PAFA.Domain.Entities;
using PAFA.Domain.IRepository;
using PAFA.Infrastructure.Persistence;

namespace PAFA.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for IngestionJob entity.
/// </summary>
 public class IngestionJobRepository(PafaDbContext ctx)
        : BaseRepository<IngestionJob>(ctx), IIngestionJobRepository
    {
        public Task<IngestionJob?> GetByPeriodAsync(int year, int month, CancellationToken ct = default)
            => _ctx.IngestionJobs
                .FirstOrDefaultAsync(j => j.ReportingPeriod == new DateOnly(year, month, 1), ct);

        public Task<IngestionJob?> GetWithFilesAsync(Guid id, CancellationToken ct = default)
            => _ctx.IngestionJobs
                .Include(j => j.IngestionFiles).ThenInclude(f => f.ValidationErrors)
                .FirstOrDefaultAsync(j => j.Id == id, ct);

        public Task<IReadOnlyList<MetricDefinition>> GetMetricDefinitionsAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<MetricDefinition>>(new List<MetricDefinition>());
    }

