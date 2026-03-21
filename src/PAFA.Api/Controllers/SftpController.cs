using MediatR;
using Microsoft.AspNetCore.Mvc;
using PAFA.Extraction.Commands.Sftp;

namespace PAFA.Api.Controllers;

[ApiController]
[Route("api/sftp")]
public class SftpController : ControllerBase
{
    private readonly IMediator _mediator;
    public SftpController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// POST /api/sftp/ingest?year=2025&amp;month=2
    /// Déclenche manuellement le téléchargement SFTP + import.
    /// Pour le POC : appel depuis Swagger à chaque test.
    /// En production : appelé par MonthlyIngestionService.
    /// </summary>
    [HttpPost("ingest")]
    public async Task<IActionResult> Ingest(
        [FromQuery] int year,
        [FromQuery] int month,
        CancellationToken ct = default)
    {
        if (year < 2020 || year > 2030) return BadRequest("Année invalide.");
        if (month < 1 || month > 12) return BadRequest("Mois invalide.");

        var result = await _mediator.Send(
            new DownloadParrFilesCommand(year, month), ct);

        if (!result.Success && !result.ImportedFiles.Any())
            return StatusCode(500, result);

        return result.FilesFailed > 0
            ? StatusCode(207, result)  // 207 Multi-Status — partial success
            : Ok(result);
    }
}
