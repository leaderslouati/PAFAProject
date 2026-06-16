// ═══════════════════════════════════════════════════════════
// PAFA.Domain/Entities/Referential/MetricDefinition.cs
//
// MANQUANT — À CRÉER
// Catalogue des ~45 MetricCodes du modèle EAV (metric_values).
// Chaque MetricCode est rattaché à un ReportCode.
// Utilisé par les SPs SQL et les parseurs ETL pour valider
// que le code métrique est connu avant insertion.
// ═══════════════════════════════════════════════════════════
namespace PAFA.Domain.Entities.Referential;

/// <summary>
/// Définition d'une métrique PARR (un KPI stocké dans metric_values).
/// Une ligne = un MetricCode (ex. "EST_PCT", "READ_PERF_PCT", "AQ_AT_RISK_GWH").
/// Rattachée à un ReportDefinition via ReportCode (FK).
/// </summary>
public class MetricDefinition
{
    /// <summary>
    /// Code unique de la métrique.
    /// Exemples : "EST_PCT", "CHECK_COUNT", "READ_PERF_PCT", "AQ_CORR_REASON_01".
    /// Clé primaire VARCHAR(50).
    /// </summary>
    public string MetricCode { get; set; } = null!;

    /// <summary>FK → ReportDefinition.ReportCode.</summary>
    public string ReportCode { get; set; } = null!;

    /// <summary>
    /// Libellé lisible affiché dans les rapports.
    /// Exemple : "Percentage of Estimated Reads (PC1)"
    /// </summary>
    public string Label { get; set; } = null!;

    /// <summary>
    /// Unité de mesure de la métrique.
    /// Valeurs : "Pct" | "Count" | "GWh" | "kWh".
    /// Utilisée pour formater l'affichage dans les RDL.
    /// </summary>
    public string Unit { get; set; } = null!;

    /// <summary>
    /// Dimension extra optionnelle portée par cette métrique.
    /// Valeurs possibles : "AgeBucket" | "EUC" | "ObligationType" | "ReasonCode" | "YearBand" | "MRECode".
    /// NULL si la métrique n'a pas de dimension supplémentaire.
    /// </summary>
    public string? ExtraDimCategory { get; set; }

    // ── Navigation ──────────────────────────────────────────────────────────
    /// <summary>Rapport parent auquel appartient cette métrique.</summary>
    public ReportDefinition Report { get; set; } = null!;
}
