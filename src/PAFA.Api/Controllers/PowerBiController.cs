using MediatR;
using Microsoft.AspNetCore.Mvc;
using PAFA.Reports.Queries.PowerBi; // <-- Changement de namespace ici !
using System.Threading.Tasks;

namespace PAFA.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PowerBiController : ControllerBase
    {
        private readonly IMediator _mediator;

        public PowerBiController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("metrics")]
        public async Task<IActionResult> GetMetrics([FromQuery] int? year, [FromQuery] int? month)
        {
            var query = new GetMetricsQuery(year, month);
            var result = await _mediator.Send(query);

            return Ok(result);
        }
    }
}