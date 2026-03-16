using Microsoft.EntityFrameworkCore;
using PAFA.Domain.Entities;
using PAFA.Domain.IRepository;
using PAFA.Infrastructure.Persistence;
using PAFA.Infrastructure.Repositories;

namespace PAFA.Infrastructure.Repository;

public class MetricValueRepository(PafaDbContext ctx)
    : BaseRepository<MetricValue>(ctx), IMetricValueRepository
{
    public async Task AddRangeAsync(
        IEnumerable<MetricValue> metrics, CancellationToken ct = default)
        => await ctx.MetricValues.AddRangeAsync(metrics, ct);

    public async Task<List<MetricValue>> GetFilteredAsync(
        int? year, int? month,
        string? metricKey = null,
        string? shipperShortCode = null,
        CancellationToken ct = default)
    {
        var q = ctx.MetricValues.AsQueryable();

        if (year.HasValue && month.HasValue)
            q = q.Where(m => m.ReportingPeriod == new DateOnly(year.Value, month.Value, 1));
        else if (year.HasValue)
            q = q.Where(m => m.ReportingPeriod.Year == year.Value);

        if (!string.IsNullOrWhiteSpace(metricKey))
            q = q.Where(m => m.MetricKey == metricKey);

        if (!string.IsNullOrWhiteSpace(shipperShortCode))
            q = q.Where(m => m.ShipperShortCode == shipperShortCode);

        return await q
            .OrderByDescending(m => m.ReportingPeriod)
            .ThenBy(m => m.ShipperShortCode)
            .ThenBy(m => m.MetricKey)
            .ToListAsync(ct);
    }

    public async Task<List<DateOnly>> GetDistinctPeriodsAsync(CancellationToken ct = default)
        => await ctx.MetricValues
            .Select(m => m.ReportingPeriod)
            .Distinct()
            .OrderByDescending(d => d)
            .Take(24)
            .ToListAsync(ct);
}