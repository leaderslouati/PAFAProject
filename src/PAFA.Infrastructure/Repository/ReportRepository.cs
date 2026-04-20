using Microsoft.EntityFrameworkCore;
using PAFA.Domain.Entities;
using PAFA.Domain.IRepository;
using PAFA.Infrastructure.Persistence;

namespace PAFA.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for Report entity.
/// </summary>
public class ReportRepository(PafaDbContext ctx)
    : BaseRepository<Report>(ctx), IReportRepository
{
    public async Task<IReadOnlyList<Report>> GetByPeriodAsync(
        DateOnly reportingPeriod, CancellationToken ct = default)
    {
        return await ctx.Set<Report>()
            .Where(r => r.ReportingPeriod == reportingPeriod && !r.IsDeleted)
            .OrderBy(r => r.ScheduleNumber)
            .ToListAsync(ct);
    }

    public async Task<Report?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await ctx.Set<Report>()
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, ct);
    }
}
