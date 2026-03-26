using MediatR;
using Microsoft.AspNetCore.Mvc;
using PAFA.Extraction.Commands.SharePoint;

namespace PAFA.Api.Controllers;

[ApiController]
[Route("api/ingest")]
public class IngestionController : ControllerBase
{
    private readonly IMediator _mediator;
    public IngestionController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// POST /api/ingest
    /// Déclenche manuellement le téléchargement SharePoint + import.
    /// Si year/month ne sont pas fournis, la période est détectée
    /// automatiquement depuis le nom de chaque fichier Xoserve.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Ingest(
        [FromQuery] int? year = null,
        [FromQuery] int? month = null,
        CancellationToken ct = default)
    {
        if (year.HasValue && (year < 2020 || year > 2040))
            return BadRequest("Année invalide.");
        if (month.HasValue && (month < 1 || month > 12))
            return BadRequest("Mois invalide.");

        var result = await _mediator.Send(
            new DownloadParrFilesCommand(year, month), ct);

        if (!result.Success && !result.ImportedFiles.Any())
            return StatusCode(500, result);

        return result.FilesFailed > 0
            ? StatusCode(207, result)
            : Ok(result);
    }
}
