// ═══════════════════════════════════════════════════════════
// PAFA.Domain/Interfaces/IPowerBiExportService.cs
// PURPOSE: Contract for generating Power BI embed tokens and
//          triggering server-side report exports (PDF/PPTX).
// ═══════════════════════════════════════════════════════════
namespace PAFA.Domain.Interfaces;

/// <summary>
/// Report audience determines which Power BI dataset/report is used
/// and whether EffectiveIdentity (RLS) is applied.
/// </summary>
public enum PbiReportAudience
{
    /// <summary>
    /// Schedule 2A — anonymised. Uses v_parr_industry dataset.
    /// EffectiveIdentity username = AliasCode (Shipper role).
    /// </summary>
    Industry = 0,

    /// <summary>
    /// Schedule 2B — non-anonymised. Uses v_parr_pac dataset.
    /// No RLS filter — PAC / PAFA admin access.
    /// </summary>
    Pac = 1
}

/// <summary>
/// Result of a Power BI embed token request.
/// Used by the web frontend to render embedded reports.
/// </summary>
public record EmbedTokenResult(
    string EmbedUrl,
    string EmbedToken,
    DateTimeOffset ExpiresAt,
    string ReportId,
    string WorkspaceId);

/// <summary>
/// Supported export file formats for Power BI REST export.
/// </summary>
public enum PbiExportFormat
{
    Pdf,
    Pptx
}

public interface IPowerBiExportService
{
    /// <summary>
    /// Generates a Power BI embed token for front-end embedding.
    /// For Industry audience: applies EffectiveIdentity with the shipper's AliasCode.
    /// For PAC audience: generates an unrestricted token.
    /// </summary>
    /// <param name="audience">Industry (anon) or PAC (non-anon).</param>
    /// <param name="aliasCode">Required when audience = Industry.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<EmbedTokenResult> GenerateEmbedTokenAsync(
        PbiReportAudience audience,
        string? aliasCode,
        CancellationToken ct = default);

    /// <summary>
    /// Triggers a server-side export on the Power BI service and
    /// returns a stream of the generated file (PDF or PPTX).
    /// Polls until the export is complete or the timeout is reached.
    /// </summary>
    /// <param name="audience">Industry (anon) or PAC (non-anon).</param>
    /// <param name="format">PDF or PPTX.</param>
    /// <param name="aliasCode">Required when audience = Industry.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<(Stream Content, string FileName)> ExportReportAsync(
        PbiReportAudience audience,
        PbiExportFormat format,
        string? aliasCode,
        CancellationToken ct = default);
}
