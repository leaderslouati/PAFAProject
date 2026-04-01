namespace PAFA.Domain.Contracts;

/// <summary>
/// DTO utilisé pour l'export Power BI CSV et la génération de rapports.
/// Défini dans Domain/Contracts pour être partagé entre
/// PAFA.Extraction et PAFA.Reports sans couplage entre les deux.
/// </summary>
public record PowerBiCsvRowDto
{
    public DateOnly PeriodeDate { get; set; }
    public string ShipperCode { get; init; } = string.Empty;
    public int? ProductClass { get; init; }
    public string? MrfCode { get; init; }
    public decimal? ReadPerformancePct { get; init; }
    public decimal? EstimatedReadPct { get; init; }
    public int? AqOverdueCount { get; init; }
    public int? TotalSiteCount { get; init; }
    public bool IsIndustryAverage { get; init; }
}

/// <summary>
/// DTO du résumé KPI pour le dashboard frontend.
/// Défini dans Domain/Contracts pour être partagé entre
/// PAFA.Extraction et PAFA.Reports sans couplage entre les deux.
/// </summary>
public record DashboardSummaryDto(
    int TotalShippers,
    int CompliantCount,
    int NonCompliantCount,
    decimal AvgReadPerformance
);
