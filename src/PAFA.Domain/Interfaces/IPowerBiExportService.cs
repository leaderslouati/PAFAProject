// ═══════════════════════════════════════════════════════════
// PAFA.Domain/Interfaces/IPowerBiExportService.cs
// PURPOSE: Contract for on-demand Power BI report export
//          (embed token generation + PDF/PPTX export).
//          Used by EmbedController for the React frontend.
// ═══════════════════════════════════════════════════════════
namespace PAFA.Domain.Interfaces;

// ── Enums ────────────────────────────────────────────────────────────

/// <summary>
/// Identifies which Power BI report/dataset pair to use.
/// Industry = Schedule 2A (anonymised, uses v_parr_industry).
/// Pac      = Schedule 2B (non-anonymised, uses v_parr_pac).
/// </summary>
public enum PbiReportAudience
{
    Industry,
    Pac
}

/// <summary>Export file format for Power BI reports.</summary>
public enum PbiExportFormat
{
    Pdf,
    Pptx
}

// ── DTOs ─────────────────────────────────────────────────────────────

/// <summary>
/// Result returned when generating an embed token for the React frontend.
/// The React app passes these to powerbi-client-react to render the report.
/// </summary>
public record EmbedTokenResult(
    string Token,
    string EmbedUrl,
    string ReportId,
    string WorkspaceId,
    DateTime Expiry);

// ── Interface ────────────────────────────────────────────────────────

/// <summary>
/// On-demand Power BI operations:
///  - Generate embed token (for React embedding)
///  - Export report to PDF/PPTX (server-side)
/// </summary>
public interface IPowerBiExportService
{
    /// <summary>
    /// Generates an embed token for a Power BI report.
    /// For Industry audience, applies EffectiveIdentity with the shipper's AliasCode
    /// so that RLS filters data to that shipper only.
    /// </summary>
    /// <param name="audience">Industry (2A) or Pac (2B).</param>
    /// <param name="aliasCode">Shipper alias code — required for Industry, ignored for Pac.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<EmbedTokenResult> GenerateEmbedTokenAsync(
        PbiReportAudience audience,
        string? aliasCode,
        CancellationToken ct = default);

    /// <summary>
    /// Exports a Power BI report to a file (PDF or PPTX).
    /// For Industry audience, applies EffectiveIdentity so the exported file
    /// only contains the shipper's anonymised data.
    /// </summary>
    /// <returns>Tuple of (file stream, suggested file name).</returns>
    Task<(Stream Content, string FileName)> ExportReportAsync(
        PbiReportAudience audience,
        PbiExportFormat format,
        string? aliasCode,
        CancellationToken ct = default);
}
