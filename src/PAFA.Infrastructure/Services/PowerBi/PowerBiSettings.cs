// ═══════════════════════════════════════════════════════════
// PAFA.Infrastructure/Services/PowerBi/PowerBiSettings.cs
// PURPOSE: Strongly-typed configuration for the Power BI
//          service principal and workspace identifiers.
// ⚠️  NEVER hardcode real secrets here — use Key Vault or
//     user-secrets in development. Only placeholder values
//     belong in appsettings.json.
// ═══════════════════════════════════════════════════════════
namespace PAFA.Infrastructure.Services.PowerBi;

public sealed class PowerBiSettings
{
    public const string SectionName = "PowerBi";

    // ── Azure AD service principal ───────────────────────────
    /// <summary>Azure AD Tenant ID (GUID).</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>App Registration Client ID (GUID).</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Client secret — store in Key Vault, not in source.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    // ── Power BI workspace ───────────────────────────────────
    /// <summary>Power BI workspace (group) GUID.</summary>
    public string WorkspaceId { get; set; } = string.Empty;

    // ── Anonymised dataset / report (Schedule 2A) ───────────
    /// <summary>
    /// Power BI Report ID for the anonymised report (v_parr_industry).
    /// Audience: Industry / Shipper role.
    /// </summary>
    public string AnonymizedReportId { get; set; } = string.Empty;

    /// <summary>Dataset ID backing the anonymised report.</summary>
    public string AnonymizedDatasetId { get; set; } = string.Empty;

    // ── Non-anonymised dataset / report (Schedule 2B) ────────
    /// <summary>
    /// Power BI Report ID for the non-anonymised report (v_parr_pac).
    /// Audience: PAC Members, PafaAdmin, PafaUser.
    /// </summary>
    public string NonAnonymizedReportId { get; set; } = string.Empty;

    /// <summary>Dataset ID backing the non-anonymised report.</summary>
    public string NonAnonymizedDatasetId { get; set; } = string.Empty;

    // ── Operational limits ───────────────────────────────────
    /// <summary>
    /// How long (seconds) to wait for a Power BI export to complete
    /// before timing out. Default: 300 s (5 minutes).
    /// </summary>
    public int ExportTimeoutSeconds { get; set; } = 300;

    /// <summary>Polling interval (seconds) while waiting for export. Default: 5 s.</summary>
    public int ExportPollIntervalSeconds { get; set; } = 5;
}
