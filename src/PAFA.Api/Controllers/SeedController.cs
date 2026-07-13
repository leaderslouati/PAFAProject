using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PAFA.Extraction.Commands.Pipeline;

namespace PAFA.Api.Controllers;

/// <summary>
/// Endpoints for seeding reference / master data into the database.
/// </summary>
[ApiController]
[Route("api/seed")]
[Authorize(Roles = "PafaAdmin")]
public sealed class SeedController : ControllerBase
{
    private readonly IMediator _mediator;

    public SeedController(IMediator mediator) => _mediator = mediator;

    // ══════════════════════════════════════════════════════════════════════
    //  POST /api/seed/shippers
    //  Body: multipart/form-data — file + password (form field)
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Uploads the "Anonymised Shipper List" Excel file and upserts all shippers
    /// into the database.
    ///
    /// The file is password-protected; supply the password via the
    /// <c>password</c> form field.  The password is transmitted over HTTPS only
    /// and is never persisted — it is used solely to decrypt the workbook in memory.
    ///
    /// Idempotent: existing shippers (matched by ShortCode) are updated;
    /// new shippers are inserted.
    /// </summary>
    /// <param name="file">The Anonymised Shipper List .xlsx file.</param>
    /// <param name="password">Workbook decryption password.</param>
    [HttpPost("shippers")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(SeedShippersResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SeedShippers(
        IFormFile file,
        [FromForm] string password,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest("No file uploaded.");

        if (string.IsNullOrWhiteSpace(password))
            return BadRequest("Password is required to decrypt the shipper list file.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not ".xlsx" and not ".xls")
            return BadRequest("Only .xlsx / .xls files are accepted.");

        await using var stream = file.OpenReadStream();

        var cmd = new SeedShippersFromFileCommand(
            FileStream:    stream,
            Password:      password,
            CorrelationId: Guid.NewGuid());

        var result = await _mediator.Send(cmd, ct);

        if (!result.Success)
            return StatusCode(StatusCodes.Status500InternalServerError,
                new SeedShippersResponse(result.Success, result.TotalParsed,
                    result.Inserted, result.Updated, result.ErrorMessage));

        return Ok(new SeedShippersResponse(result.Success, result.TotalParsed,
            result.Inserted, result.Updated, result.ErrorMessage));
    }
}

// ── Response DTO ──────────────────────────────────────────────────────────────

public sealed record SeedShippersResponse(
    bool    Success,
    int     TotalParsed,
    int     Inserted,
    int     Updated,
    string? ErrorMessage);
