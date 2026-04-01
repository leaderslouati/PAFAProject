using MediatR;
using Microsoft.AspNetCore.Mvc;
using PAFA.Extraction.Commands.Import;

namespace PAFA.Api.Controllers.ImportFile;

[ApiController]
[Route("api/processing")]
public class ProcessingController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProcessingController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// POST /api/processing/{fileId}/process
    /// Parse, validate and persist a previously uploaded file.
    /// On success  → file is moved to MinIO bucket "processed/{year}/{month}/".
    /// On failure  → file is moved to MinIO bucket "failed/{year}/{month}/".
    /// </summary>
    [HttpPost("{fileId:guid}/process")]
    [ProducesResponseType(typeof(ProcessFileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProcessFileResult), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ProcessFile(Guid fileId, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ProcessFileCommand(fileId), ct);

        return result.Success ? Ok(result) : BadRequest(result);
    }
}

