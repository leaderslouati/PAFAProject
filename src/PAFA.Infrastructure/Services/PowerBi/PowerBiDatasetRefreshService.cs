// ═══════════════════════════════════════════════════════════
// PAFA.Infrastructure/Services/PowerBi/PowerBiDatasetRefreshService.cs
// PURPOSE: Triggers and monitors Import-mode dataset refreshes
//          using the Power BI REST API polling pattern.
//
//          Auth is delegated to PowerBiClientFactory (MSAL
//          Service Principal). Refresh is separate from export
//          per the user's separation-of-concerns requirement.
// ═══════════════════════════════════════════════════════════
using Microsoft.Extensions.Logging;
using Microsoft.PowerBI.Api;
using Microsoft.PowerBI.Api.Models;

namespace PAFA.Infrastructure.Services.PowerBi;

/// <summary>
/// Handles dataset refresh (Import mode) with asynchronous polling.
/// Separated from the export logic for clear responsibility boundaries.
/// </summary>
public sealed class PowerBiDatasetRefreshService(
    PowerBiClientFactory factory,
    PowerBiSettings powerBiSettings,
    PowerBiBatchExportSettings batchSettings,
    ILogger<PowerBiDatasetRefreshService> logger)
{
    /// <summary>
    /// Refreshes all datasets that have <see cref="DatasetDefinition.RequiresRefresh"/> = true.
    /// Datasets are refreshed sequentially (Power BI allows 1 concurrent refresh per dataset).
    /// </summary>
    public async Task RefreshAllDatasetsAsync(
        IReadOnlyList<DatasetDefinition> datasets,
        CancellationToken ct)
    {
        var toRefresh = datasets.Where(d => d.RequiresRefresh).ToList();

        if (toRefresh.Count == 0)
        {
            logger.LogInformation("No datasets require refresh — skipping.");
            return;
        }

        logger.LogInformation("Refreshing {Count} dataset(s)…", toRefresh.Count);

        foreach (var ds in toRefresh)
        {
            // Skip placeholder / unconfigured DatasetIds (not yet filled in appsettings)
            if (string.IsNullOrWhiteSpace(ds.DatasetId)
                || ds.DatasetId.StartsWith('<')
                || !Guid.TryParse(ds.DatasetId, out _))
            {
                logger.LogWarning(
                    "Skipping dataset '{Label}' — DatasetId '{Id}' is not a valid GUID. " +
                    "Fill in PowerBiBatchExport:Datasets in appsettings.",
                    ds.Label, ds.DatasetId);
                continue;
            }

            await RefreshDatasetAsync(ds, ct);
        }

        logger.LogInformation("All {Count} dataset refresh(es) completed.", toRefresh.Count);
    }

    /// <summary>
    /// Triggers a single dataset refresh and polls until Completed, Failed, or timeout.
    /// Timeout: <see cref="PowerBiBatchExportSettings.DatasetRefreshTimeoutMinutes"/> (default 10 min).
    /// </summary>
    public async Task RefreshDatasetAsync(DatasetDefinition dataset, CancellationToken ct)
    {
        var client  = await factory.CreateAsync(ct);
        var groupId = Guid.Parse(powerBiSettings.WorkspaceId);

        logger.LogInformation(
            "Triggering dataset refresh: {Label} ({DatasetId})",
            dataset.Label, dataset.DatasetId);

        // ── 1. Trigger the refresh ──────────────────────────────────
        await client.Datasets.RefreshDatasetInGroupAsync(
            groupId,
            dataset.DatasetId,
            new DatasetRefreshRequest { NotifyOption = "NoNotification" });

        // ── 2. Initial delay to let the refresh register ────────────
        await Task.Delay(TimeSpan.FromSeconds(5), ct);

        // ── 3. Poll until done or timeout ───────────────────────────
        var deadline     = DateTime.UtcNow.AddMinutes(batchSettings.DatasetRefreshTimeoutMinutes);
        var pollInterval = TimeSpan.FromSeconds(batchSettings.RefreshPollIntervalSeconds);

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            var history = await client.Datasets
                .GetRefreshHistoryInGroupAsync(groupId, dataset.DatasetId, top: 1);

            var latest = history.Value.FirstOrDefault();
            if (latest is null)
            {
                await Task.Delay(pollInterval, ct);
                continue;
            }

            logger.LogDebug(
                "Refresh poll — {Label}: Status={Status} Progress={Pct}%",
                dataset.Label, latest.Status,
                latest.EndTime.HasValue ? 100 : 0);

            switch (latest.Status)
            {
                case "Completed":
                    var elapsed = latest.EndTime.HasValue && latest.StartTime.HasValue
                        ? (latest.EndTime.Value - latest.StartTime.Value).ToString(@"mm\:ss")
                        : "N/A";
                    logger.LogInformation(
                        "Dataset refresh completed: {Label} (elapsed {Elapsed})",
                        dataset.Label, elapsed);
                    return;

                case "Failed":
                    throw new InvalidOperationException(
                        $"Dataset refresh failed for '{dataset.Label}'. " +
                        $"ServiceException: {latest.ServiceExceptionJson}");

                case "Cancelled":
                case "Disabled":
                    throw new InvalidOperationException(
                        $"Dataset refresh {latest.Status} for '{dataset.Label}'.");
            }

            // "Unknown" = still in progress → keep polling
            await Task.Delay(pollInterval, ct);
        }

        throw new TimeoutException(
            $"Dataset refresh for '{dataset.Label}' timed out after " +
            $"{batchSettings.DatasetRefreshTimeoutMinutes} minutes.");
    }
}
