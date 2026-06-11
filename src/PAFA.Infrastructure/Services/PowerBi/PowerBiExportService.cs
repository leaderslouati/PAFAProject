// ═══════════════════════════════════════════════════════════
// PAFA.Infrastructure/Services/PowerBi/PowerBiExportService.cs
// PURPOSE: On-demand embed-token generation and PDF/PPTX export
//          for the React frontend (EmbedController).
//
//  Key design decisions:
//   - Industry (2A): EffectiveIdentity.Username = AliasCode
//     → Power BI RLS filters to that shipper only.
//     → The underlying dataset queries v_parr_industry which
//       exposes alias_code (NEVER real_shipper_name).
//   - PAC (2B): No EffectiveIdentity — admin sees all data.
//     → The underlying dataset queries v_parr_pac which
//       exposes real_shipper_name.
// ═══════════════════════════════════════════════════════════
using Microsoft.Extensions.Logging;
using Microsoft.PowerBI.Api;
using Microsoft.PowerBI.Api.Models;
using PAFA.Domain.Interfaces;

namespace PAFA.Infrastructure.Services.PowerBi;

public sealed class PowerBiExportService(
    PowerBiClientFactory factory,
    PowerBiSettings settings,
    ILogger<PowerBiExportService> logger) : IPowerBiExportService
{
    // ─────────────────────────────────────────────────────────────────
    //  EMBED TOKEN — for React powerbi-client-react
    // ─────────────────────────────────────────────────────────────────

    public async Task<EmbedTokenResult> GenerateEmbedTokenAsync(
        PbiReportAudience audience,
        string? aliasCode,
        CancellationToken ct = default)
    {
        var (reportId, datasetId) = ResolveIds(audience);
        var groupId = Guid.Parse(settings.WorkspaceId);
        var rptGuid = Guid.Parse(reportId);

        using var client = await factory.CreateAsync(ct);

        // Build token request — with or without EffectiveIdentity
        var tokenRequest = BuildTokenRequest(audience, datasetId, aliasCode);

        var embedToken = await client.Reports
            .GenerateTokenInGroupAsync(groupId, rptGuid, tokenRequest, ct);

        // Retrieve embed URL from report metadata
        var report = await client.Reports.GetReportInGroupAsync(groupId, rptGuid, ct);

        logger.LogInformation(
            "Embed token generated — Audience={Audience} AliasCode={Alias} Expiry={Expiry}",
            audience, aliasCode ?? "(none)", embedToken.Expiration);

        return new EmbedTokenResult(
            Token: embedToken.Token,
            EmbedUrl: report.EmbedUrl,
            ReportId: reportId,
            WorkspaceId: settings.WorkspaceId,
            Expiry: embedToken.Expiration);
    }

    // ─────────────────────────────────────────────────────────────────
    //  EXPORT — server-side PDF / PPTX
    // ─────────────────────────────────────────────────────────────────

    public async Task<(Stream Content, string FileName)> ExportReportAsync(
        PbiReportAudience audience,
        PbiExportFormat format,
        string? aliasCode,
        CancellationToken ct = default)
    {
        var (reportId, _) = ResolveIds(audience);
        var groupId = Guid.Parse(settings.WorkspaceId);
        var rptGuid = Guid.Parse(reportId);

        using var client = await factory.CreateAsync(ct);

        // Map format
        var pbiFormat = format == PbiExportFormat.Pdf
            ? FileFormat.PDF
            : FileFormat.PPTX;

        var exportRequest = new ExportReportRequest { Format = pbiFormat };

        // Kick off export
        var exportJob = await client.Reports
            .ExportToFileInGroupAsync(groupId, rptGuid, exportRequest, ct);

        logger.LogInformation(
            "Export started — Audience={Audience} Format={Format} ExportId={ExportId}",
            audience, format, exportJob.Id);

        // Poll until complete
        var stream = await PollExportAsync(client, groupId, rptGuid, exportJob.Id, ct);

        var ext = format == PbiExportFormat.Pdf ? ".pdf" : ".pptx";
        var scheduleLabel = audience == PbiReportAudience.Industry ? "2A" : "2B";
        var fileName = $"PAFA_Schedule_{scheduleLabel}_{DateTime.UtcNow:yyyyMMdd_HHmm}{ext}";

        return (stream, fileName);
    }

    // ─────────────────────────────────────────────────────────────────
    //  PRIVATE HELPERS
    // ─────────────────────────────────────────────────────────────────

    private (string ReportId, string DatasetId) ResolveIds(PbiReportAudience audience) =>
        audience switch
        {
            PbiReportAudience.Industry => (settings.AnonymizedReportId, settings.AnonymizedDatasetId),
            PbiReportAudience.Pac     => (settings.NonAnonymizedReportId, settings.NonAnonymizedDatasetId),
            _ => throw new ArgumentOutOfRangeException(nameof(audience))
        };

    private static GenerateTokenRequest BuildTokenRequest(
        PbiReportAudience audience, string datasetId, string? aliasCode)
    {
        if (audience == PbiReportAudience.Industry && !string.IsNullOrWhiteSpace(aliasCode))
        {
            // RLS: Username = AliasCode → USERPRINCIPALNAME() in the Power BI model
            return new GenerateTokenRequest(
                accessLevel: "View",
                identities:
                [
                    new EffectiveIdentity(
                        username: aliasCode,
                        datasets: [datasetId])
                ]);
        }

        // PAC (admin) — no RLS filter
        return new GenerateTokenRequest(accessLevel: "View");
    }

    private async Task<Stream> PollExportAsync(
        PowerBIClient client, Guid groupId, Guid reportId, string exportId,
        CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddSeconds(settings.ExportTimeoutSeconds);
        var pollMs = settings.ExportPollIntervalSeconds * 1_000;

        Export? status = null;
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(pollMs, ct);

            status = await client.Reports
                .GetExportToFileStatusInGroupAsync(groupId, reportId, exportId, ct);

            logger.LogDebug(
                "Export poll — ExportId={ExportId} Status={Status} Pct={Pct}%",
                exportId, status.Status, status.PercentComplete);

            if (status.Status is ExportState.Succeeded or ExportState.Failed)
                break;
        }

        if (status?.Status != ExportState.Succeeded)
        {
            throw new InvalidOperationException(
                $"Power BI export did not complete. " +
                $"Status: {status?.Status?.ToString() ?? "Timeout"}. " +
                $"ExportId: {exportId}");
        }

        return await client.Reports
            .GetFileOfExportToFileInGroupAsync(groupId, reportId, exportId, ct);
    }
}
