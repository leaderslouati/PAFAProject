using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PAFA.Domain.Interfaces;

namespace PAFA.Api.Controllers;

/// <summary>
/// Power BI Embedded endpoints for the React frontend.
/// - GET  /api/embed/token   → returns an embed token + URL for powerbi-client-react
/// - POST /api/embed/export  → triggers a server-side PDF/PPTX export and returns the file
/// </summary>
[ApiController]
[Route("api/embed")]
[Authorize]
public class EmbedController(IPowerBiExportService pbiService) : ControllerBase
{
    // ─────────────────────────────────────────────────────────────────
    //  GET /api/embed/token?audience=Industry&aliasCode=SH_001
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a Power BI embed token for the React frontend.
    /// The React app uses this to render the report via powerbi-client-react.
    /// </summary>
    /// <param name="audience">Industry (Schedule 2A, anonymised) or Pac (Schedule 2B).</param>
    /// <param name="aliasCode">Required for Industry audience — the shipper's AliasCode.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Embed token, embed URL, report ID, workspace ID, expiry.</response>
    /// <response code="400">Missing aliasCode for Industry audience.</response>
    [HttpGet("token")]
    [ProducesResponseType(typeof(EmbedTokenResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetEmbedToken(
        [FromQuery] PbiReportAudience audience,
        [FromQuery] string? aliasCode,
        CancellationToken ct = default)
    {
        if (audience == PbiReportAudience.Industry
            && string.IsNullOrWhiteSpace(aliasCode))
        {
            return BadRequest("aliasCode is required for Industry (Schedule 2A) reports.");
        }

        var result = await pbiService.GenerateEmbedTokenAsync(audience, aliasCode, ct);
        return Ok(result);
    }

    // ─────────────────────────────────────────────────────────────────
    //  POST /api/embed/export
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Triggers a server-side Power BI export (PDF or PPTX) and returns the file.
    /// This is called by the React "Download PDF" / "Download PPTX" buttons.
    /// The export is asynchronous on Power BI's side — this endpoint polls until done.
    /// </summary>
    /// <param name="request">Audience, format, and optional aliasCode.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">The exported file stream.</response>
    /// <response code="400">Missing aliasCode for Industry audience.</response>
    [HttpPost("export")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExportReport(
        [FromBody] ExportRequest request,
        CancellationToken ct = default)
    {
        if (request.Audience == PbiReportAudience.Industry
            && string.IsNullOrWhiteSpace(request.AliasCode))
        {
            return BadRequest("aliasCode is required for Industry (Schedule 2A) exports.");
        }

        var (content, fileName) = await pbiService.ExportReportAsync(
            request.Audience, request.Format, request.AliasCode, ct);

        var contentType = request.Format == PbiExportFormat.Pdf
            ? "application/pdf"
            : "application/vnd.openxmlformats-officedocument.presentationml.presentation";

        return File(content, contentType, fileName);
    }
}

/// <summary>Request body for Power BI report export.</summary>
public record ExportRequest(
    PbiReportAudience Audience,
    PbiExportFormat Format = PbiExportFormat.Pdf,
    string? AliasCode = null);
