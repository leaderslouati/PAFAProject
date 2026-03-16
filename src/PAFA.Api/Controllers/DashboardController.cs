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

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary([FromQuery] int? year, [FromQuery] int? month)
        {
            var result = await _mediator.Send(new GetDashboardSummaryQuery(year, month));
            return Ok(result);
        }
    }
}