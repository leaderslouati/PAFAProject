using MediatR;
using PAFA.Domain.Contracts;
using PAFA.Domain.Entities;
using PAFA.Domain.Enums;
using PAFA.Domain.Interfaces;
using PAFA.Domain.IRepository;
using PAFA.Reports.Queries;

namespace PAFA.Reports.Handlers;

public  class ExportPowerBiCsvQueryHandler
    : IRequestHandler<ExportPowerBiCsvQuery, Stream>
{
    private readonly IMetricValueRepository _repo;
    private readonly IEnumerable<IReportWriter> _writers;

    public ExportPowerBiCsvQueryHandler(
        IMetricValueRepository repo, IEnumerable<IReportWriter> writers)
    { _repo = repo; _writers = writers; }

    public async Task<Stream> Handle(ExportPowerBiCsvQuery q, CancellationToken ct)
    {
        var metrics = await _repo.GetFilteredAsync(q.PeriodYear, q.PeriodMonth, null, null, ct);

        var rows = metrics
          .GroupBy(m => new { m.ShipperShortCode, m.ReportingPeriod })
          .Select(g => new PowerBiCsvRowDto
          {
              PeriodeDate = g.Key.ReportingPeriod,
              ShipperCode = g.Key.ShipperShortCode,
              ReadPerformancePct = Val(g, "readperformancepct"),
              EstimatedReadPct = Val(g, "estimatedreadpct"),
              AqOverdueCount = (int?)Val(g, "aqoverduecount"),
              TotalSiteCount = (int?)Val(g, "totalsitecount"),
              ProductClass = (int?)Val(g, "productclass"),
              IsIndustryAverage = false
          }).ToList();

        static decimal? Val(IGrouping<dynamic, MetricValue> g, string key)
            => g.FirstOrDefault(m =>
                m.MetricKey.Equals(key, StringComparison.OrdinalIgnoreCase))?.Value;
        var writer = _writers.SingleOrDefault(w => w.Format == ExportFormat.Csv)
            ?? throw new InvalidOperationException("Aucun CsvReportWriter enregistré en DI.");

        return await writer.WriteAsync(rows, ct);
    }
}