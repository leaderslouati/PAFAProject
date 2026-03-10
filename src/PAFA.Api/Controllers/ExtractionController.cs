using MassTransit.Mediator;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using PAFA.Extraction.Commands;
using IMediator = MediatR.IMediator;


namespace PAFA.Api.Controllers
{
    [ApiController]
    [Route("api/extract")]
    public class ExtractionController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ExtractionController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost("simulate-download")]
        public async Task<IActionResult> SimulateDownload([FromQuery] string fileName)
        {
            var command = new IngestFileCommand(fileName);
            var fileId = await _mediator.Send(command);

            return Ok(new { Message = "File extraction initiated", FileId = fileId });
        }
    }
}