using MediatR;
using Microsoft.AspNetCore.Mvc;
using PAFA.Reports.Dashboard.Queries;

namespace PAFA.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController(IMediator mediator) : ControllerBase
    {
        private readonly IMediator _mediator = mediator;

        /// <summary>
        /// GET /api/dashboard/summary?year=2025&amp;month=2
        /// Retourne un résumé du dashboard : nombre de shippers, conformité PC1, moyenne Read Performance.
        /// Si year/month non fournis, calcule sur toutes les données disponibles.
        /// </summary>
        [HttpGet("summary")]
        [ProducesResponseType(typeof(PAFA.Domain.Contracts.DashboardSummaryDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetSummary([FromQuery] int? year, [FromQuery] int? month)
        {
            var result = await _mediator.Send(new GetDashboardSummaryQuery(year, month));
            return Ok(result);
        }
    }
}