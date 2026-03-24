using MediatR;
using PAFA.Domain.Contracts;

namespace PAFA.Reports.Dashboard.Queries;

public record GetDashboardSummaryQuery(int? Year, int? Month)
    : IRequest<DashboardSummaryDto>;