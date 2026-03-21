using MediatR;
using Microsoft.AspNetCore.Mvc;
using PAFA.Domain.IRepository;
using PAFA.Domain.Repositories;
using PAFA.Extraction.Commands.Import;

namespace PAFA.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImportController(IMediator mediator, IUnitOfWork uow) : ControllerBase
{
    [HttpPost("upload")]
    public async Task<IActionResult> UploadFile(
        IFormFile file,
        [FromForm] int periodYear,
        [FromForm] int periodMonth)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Le fichier est vide ou invalide.");

        byte[] fileBytes;
        using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms);
            fileBytes = ms.ToArray();
        }

        var command = new UploadParrFilesCommand(
            file.FileName, fileBytes,
            periodYear, periodMonth, "User_POC");

        var result = await mediator.Send(command);

        return result.Success ? Accepted(result) : BadRequest(result.ErrorMessage);
    }

    // ── Endpoint erreurs de validation ────────────────────────────
    [HttpGet("{fileId}/errors")]
    public async Task<IActionResult> GetValidationErrors(Guid fileId)
    {
        // IIngestionFileRepository n'a pas GetValidationErrorsAsync — 
        // on passe par IBaseRepository
        var errors = await uow.IngestionFiles
            .FindAsync(e => e.Id == fileId);

        if (!errors.Any()) return NotFound();

        var file = errors.First();
        return Ok(file.ValidationErrors);
    }
}