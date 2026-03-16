using MediatR;
using PAFA.Domain.Enums;
using PAFA.Domain.IRepository;
using PAFA.Extraction.Commands.Export;
using PAFA.Extraction.Reports.Interfaces;
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

        var rows = metrics.Select(m => new PowerBiCsvRowDto
        {
            PeriodeDate = m.ReportingPeriod, 
            ShipperCode = m.ShipperShortCode            
        }).ToList();

        var writer = _writers.SingleOrDefault(w => w.Format == ExportFormat.Csv)
            ?? throw new InvalidOperationException("Aucun CsvReportWriter enregistré en DI.");

        return await writer.WriteAsync(rows, ct);
    }
}