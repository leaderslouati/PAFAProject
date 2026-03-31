using MediatR;
using Microsoft.AspNetCore.Mvc;
using PAFA.Domain.IRepository;
using PAFA.Domain.Repositories;
using PAFA.Extraction.Commands.Import;

namespace PAFA.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImportController(IMediator mediator, IUnitOfWork uow, PAFA.Domain.Interfaces.IDdpCredentialValidator ddpValidator) : ControllerBase
{
    [HttpPost("upload")]
    public async Task<IActionResult> UploadFile(
        IFormFile file,
        [FromForm] int periodYear,
        [FromForm] int periodMonth,
        [FromForm] string? sourceSystem,
        [FromForm] string? ddpUsername,
        [FromForm] string? ddpToken)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Le fichier est vide ou invalide.");

        byte[] fileBytes;
        using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms);
            fileBytes = ms.ToArray();
        }

        // If source is DDP, validate provided credentials before ingest
        if (!string.IsNullOrWhiteSpace(sourceSystem) && sourceSystem.Equals("DDP", StringComparison.OrdinalIgnoreCase))
        {
            var (ok, msg) = await ddpValidator.ValidateAsync(ddpUsername, ddpToken);
            if (!ok)
                return Unauthorized(new { error = "DDP credentials invalid", detail = msg });
        }

        var uploadedBy = !string.IsNullOrWhiteSpace(ddpUsername) ? ddpUsername : "User_POC";

        var command = new UploadParrFilesCommand(
            file.FileName, fileBytes,
            periodYear, periodMonth, uploadedBy, sourceSystem ?? "MANUAL");

        var result = await mediator.Send(command);

        return result.Success ? Accepted(result) : BadRequest(result.ErrorMessage);
    }

    // ── Endpoint erreurs de validation ────────────────────────────
    [HttpGet("{fileId}/errors")]
    public async Task<IActionResult> GetValidationErrors(Guid fileId)
    {
        var errors = await uow.IngestionFiles.FindAsync(e => e.Id == fileId);

        if (!errors.Any()) return NotFound();

        var file = errors.First();
        return Ok(file.ValidationErrors);
    }
}