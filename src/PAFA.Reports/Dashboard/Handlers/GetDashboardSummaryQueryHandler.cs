using MediatR;
using PAFA.Domain.Contracts;
using PAFA.Domain.IRepository;
using PAFA.Domain.Repositories;
using PAFA.Reports.Dashboard.Queries;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PAFA.Reports.Handlers
{
    public class GetDashboardSummaryQueryHandler : IRequestHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
    {
        // Seuil PC1 conformité lecture (97.5 %)
        private const decimal Pc1Threshold = 97.5m;
        private const string  ReadPerfKey  = "read_performance_pct";

        private readonly IMetricValueRepository _repo;

        public GetDashboardSummaryQueryHandler(IMetricValueRepository repo)
            => _repo = repo;

        public async Task<DashboardSummaryDto> Handle(
            GetDashboardSummaryQuery request, CancellationToken cancellationToken)
        {
            // 1. Toutes les métriques de la période demandée
            var all = await _repo.GetFilteredAsync(
                year:  request.Year,
                month: request.Month,
                ct:    cancellationToken);

            // 2. Shippers distincts
            var totalShippers = all
                .Select(m => m.ShipperShortCode)
                .Distinct()
                .Count();

            // 3. Lignes "read_performance_pct" → conformité PC1
            var readPerfRows = all
                .Where(m => m.MetricKey == ReadPerfKey)
                .ToList();

            int compliant    = readPerfRows.Count(m => m.Value >= Pc1Threshold);
            int nonCompliant = readPerfRows.Count(m => m.Value <  Pc1Threshold);

            decimal avgReadPerf = readPerfRows.Any()
                ? Math.Round(readPerfRows.Average(m => m.Value), 2)
                : 0m;

            return new DashboardSummaryDto(
                TotalShippers:      totalShippers,
                CompliantCount:     compliant,
                NonCompliantCount:  nonCompliant,
                AvgReadPerformance: avgReadPerf);
        }
    }
}