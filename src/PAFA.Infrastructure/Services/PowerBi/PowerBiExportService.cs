// ═══════════════════════════════════════════════════════════
// PAFA.Infrastructure/Services/PowerBi/PowerBiExportService.cs
// PURPOSE: Implements IPowerBiExportService.
//    - GenerateEmbedTokenAsync : produces an embed token for
//      front-end embedding, applying EffectiveIdentity (RLS)
//      for Industry (anonymised) reports.
//    - ExportReportAsync : triggers a server-side PDF/PPTX
//      export on the Power BI service and returns the stream.
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
    // GenerateEmbedTokenAsync
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<EmbedTokenResult> GenerateEmbedTokenAsync(
        PbiReportAudience audience,
        string? aliasCode,
        CancellationToken ct = default)
    {
        ValidateAudienceArguments(audience, aliasCode);

        var (reportId, datasetId) = ResolveReportIds(audience);
        var groupId = Guid.Parse(settings.WorkspaceId);
        var client = await factory.CreateAsync(ct);

        // Fetch the report metadata to get the embed URL.
        var report = await client.Reports.GetReportInGroupAsync(groupId, Guid.Parse(reportId));

        var tokenRequest = BuildTokenRequest(audience, aliasCode, datasetId);
        var tokenResponse = await client.Reports
            .GenerateTokenInGroupAsync(groupId, Guid.Parse(reportId), tokenRequest);

        logger.LogInformation(
            "Embed token generated for audience={Audience} aliasCode={Alias} expiry={Expiry}",
            audience, aliasCode ?? "N/A", tokenResponse.Expiration);

        return new EmbedTokenResult(
            EmbedUrl:    report.EmbedUrl,
            EmbedToken:  tokenResponse.Token,
            ExpiresAt:   new DateTimeOffset(tokenResponse.Expiration, TimeSpan.Zero),
            ReportId:    reportId,
            WorkspaceId: settings.WorkspaceId);
    }

    // ─────────────────────────────────────────────────────────────────
    // ExportReportAsync
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<(Stream Content, string FileName)> ExportReportAsync(
        PbiReportAudience audience,
        PbiExportFormat format,
        string? aliasCode,
        CancellationToken ct = default)
    {
        ValidateAudienceArguments(audience, aliasCode);

        var (reportId, _)  = ResolveReportIds(audience);
        var groupId        = Guid.Parse(settings.WorkspaceId);
        var fileFormat     = MapFormat(format);
        var client         = await factory.CreateAsync(ct);

        // ── 1. Kick off the export ──────────────────────────────────
        var exportRequest = BuildExportRequest(audience, aliasCode, fileFormat);
        var exportJob = await client.Reports
            .ExportToFileInGroupAsync(groupId, Guid.Parse(reportId), exportRequest);

        logger.LogInformation(
            "Power BI export started. ExportId={ExportId} Audience={Audience}",
            exportJob.Id, audience);

        // ── 2. Poll until done or timeout ──────────────────────────
        var deadline = DateTime.UtcNow.AddSeconds(settings.ExportTimeoutSeconds);
        var pollMs   = settings.ExportPollIntervalSeconds * 1_000;

        Export? status = null;
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(pollMs, ct);
            status = await client.Reports
                .GetExportToFileStatusInGroupAsync(
                    groupId, Guid.Parse(reportId), exportJob.Id);

            logger.LogDebug(
                "Export poll — ExportId={ExportId} Status={Status} Progress={Progress}%",
                exportJob.Id, status.Status, status.PercentComplete);

            if (status.Status is ExportState.Succeeded or ExportState.Failed)
                break;
        }

        if (status?.Status != ExportState.Succeeded)
        {
            throw new InvalidOperationException(
                $"Power BI export did not complete successfully. " +
                $"Final status: {status?.Status?.ToString() ?? "Timeout"}. ExportId: {exportJob.Id}");
        }

        // ── 3. Download the file stream ────────────────────────────
        var fileStream = await client.Reports
            .GetFileOfExportToFileInGroupAsync(
                groupId, Guid.Parse(reportId), exportJob.Id);

        var extension = format == PbiExportFormat.Pdf ? "pdf" : "pptx";
        var audienceLabel = audience == PbiReportAudience.Industry ? "Industry" : "PAC";
        var fileName = $"PAFA_{audienceLabel}_Report_{DateTime.UtcNow:yyyyMMdd_HHmm}.{extension}";

        logger.LogInformation(
            "Export completed. ExportId={ExportId} FileName={FileName}", exportJob.Id, fileName);

        return (fileStream, fileName);
    }

    // ─────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────

    private (string ReportId, string DatasetId) ResolveReportIds(PbiReportAudience audience)
        => audience == PbiReportAudience.Industry
            ? (settings.AnonymizedReportId,    settings.AnonymizedDatasetId)
            : (settings.NonAnonymizedReportId, settings.NonAnonymizedDatasetId);

    private static void ValidateAudienceArguments(PbiReportAudience audience, string? aliasCode)
    {
        if (audience == PbiReportAudience.Industry
            && string.IsNullOrWhiteSpace(aliasCode))
        {
            throw new ArgumentException(
                "aliasCode is required for Industry (anonymised) reports. " +
                "Each Shipper must have an AliasCode configured.",
                nameof(aliasCode));
        }
    }

    /// <summary>
    /// Builds a GenerateTokenRequest.
    /// For Industry: includes EffectiveIdentity with the AliasCode as username so
    /// the Power BI RLS filter [shipper_code] = USERPRINCIPALNAME() evaluates correctly.
    /// For PAC: no identity filter — full dataset access.
    /// </summary>
    private static GenerateTokenRequest BuildTokenRequest(
        PbiReportAudience audience,
        string? aliasCode,
        string datasetId)
    {
        if (audience == PbiReportAudience.Pac)
            return new GenerateTokenRequest(accessLevel: "View");

        var identity = new EffectiveIdentity(
            username: aliasCode!,
            datasets: [datasetId],
            roles:    ["Shipper"]);

        return new GenerateTokenRequest(
            accessLevel: "View",
            identities:  [identity]);
    }

    /// <summary>
    /// Builds an ExportReportRequest.
    /// For Industry: injects EffectiveIdentity (AliasCode) so the exported PDF
    /// is filtered to only that shipper's data via Power BI RLS.
    /// </summary>
    private static ExportReportRequest BuildExportRequest(
        PbiReportAudience audience,
        string? aliasCode,
        FileFormat fileFormat)
    {
        if (audience == PbiReportAudience.Pac)
            return new ExportReportRequest { Format = fileFormat };

        var identity = new EffectiveIdentity(
            username: aliasCode!,
            datasets: null,           // resolved at export time from the report's dataset
            roles:    ["Shipper"]);

        return new ExportReportRequest
        {
            Format                            = fileFormat,
            PowerBIReportConfiguration        = new PowerBIReportExportConfiguration
            {
                Identities = [identity]
            }
        };
    }

    private static FileFormat MapFormat(PbiExportFormat format)
        => format switch
        {
            PbiExportFormat.Pdf  => FileFormat.PDF,
            PbiExportFormat.Pptx => FileFormat.PPTX,
            _                    => FileFormat.PDF
        };
}
