using MediatR;
using PAFA.Domain.Enums;

namespace PAFA.Reports.Queries
{
    /// <summary>
    /// Query: export MetricValues for a given period as a CSV stream.
    /// ReportVariant controls whether ShipperShortCode is masked (Anonymized)
    /// or exposed as-is (Full).
    /// </summary>
    public  record ExportPowerBiCsvQuery : IRequest<Stream>
    {
        public int?          PeriodYear  { get; init; }
        public int?          PeriodMonth { get; init; }
    }
}