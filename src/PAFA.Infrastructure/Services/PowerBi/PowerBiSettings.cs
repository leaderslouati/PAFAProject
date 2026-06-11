namespace PAFA.Infrastructure.Services.PowerBi;

/// <summary>
/// Strongly-typed configuration for Power BI Service Principal authentication.
/// Bound from appsettings.json § "PowerBi".
/// </summary>
public sealed class PowerBiSettings
{
    public const string SectionName = "PowerBi";

    /// <summary>Azure AD Tenant ID.</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Azure AD App Registration (Service Principal) Client ID.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Client Secret for the Service Principal.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Power BI Workspace (Group) GUID. Appears in the URL: app.powerbi.com/groups/{GUID}/...</summary>
    public string WorkspaceId { get; set; } = string.Empty;

    // ── Schedule 2A — Anonymised (Industry) ─────────────────────
    /// <summary>Report GUID for the anonymised (Industry) report.</summary>
    public string AnonymizedReportId { get; set; } = string.Empty;

    /// <summary>Dataset GUID backing the anonymised report.</summary>
    public string AnonymizedDatasetId { get; set; } = string.Empty;

    // ── Schedule 2B — Non-Anonymised (PAC) ──────────────────────
    /// <summary>Report GUID for the non-anonymised (PAC) report.</summary>
    public string NonAnonymizedReportId { get; set; } = string.Empty;

    /// <summary>Dataset GUID backing the non-anonymised report.</summary>
    public string NonAnonymizedDatasetId { get; set; } = string.Empty;

    // ── Export Defaults ─────────────────────────────────────────
    /// <summary>Max wait for a single PDF/PPTX export (seconds). Default: 300.</summary>
    public int ExportTimeoutSeconds { get; set; } = 300;

    /// <summary>Polling interval while waiting for export (seconds). Default: 5.</summary>
    public int ExportPollIntervalSeconds { get; set; } = 5;

    /// <summary>Authority URL. Defaults to Azure public cloud.</summary>
    public string Authority => $"https://login.microsoftonline.com/{TenantId}";

    /// <summary>Power BI API scope for Service Principal auth.</summary>
    public string[] Scopes { get; set; } = ["https://analysis.windows.net/powerbi/api/.default"];
}
