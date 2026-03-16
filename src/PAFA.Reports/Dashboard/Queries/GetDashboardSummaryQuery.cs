using MediatR;
using PAFA.Domain.IRepository;
using PAFA.Extraction.Commands.Export;

namespace PAFA.Reports.Dashboard.Queries;

public record GetDashboardSummaryQuery(int? Year, int? Month)
    : IRequest<DashboardSummaryDto>;