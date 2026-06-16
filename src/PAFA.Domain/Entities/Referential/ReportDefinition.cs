// ═══════════════════════════════════════════════════════════
// PAFA.Domain/Entities/Referential/ReportDefinition.cs
//
// MANQUANT — À CRÉER
// Catalogue des 22+ rapports PARR (2A.1→2A.19 + 2B exclusifs).
// Utilisé par les SPs SQL pour paramétrer les requêtes sans hardcoder
// le ReportCode dans chaque procédure.
// ═══════════════════════════════════════════════════════════
namespace PAFA.Domain.Entities.Referential;

/// <summary>
/// Définition d'un rapport PARR du catalogue Schedule 2A ou 2B.
/// Une ligne = un rapport (ex. "2A.1", "2A.5", "2B.11").
/// Seedée avec 23 lignes au démarrage (migration EF Core).
/// </summary>
public class ReportDefinition
{
    /// <summary>
    /// Code unique du rapport.
    /// Exemples : "2A.1", "2A.5", "2B.11", "2A.11a", "2A.11b".
    /// Clé primaire VARCHAR(10).
    /// </summary>
    public string ReportCode { get; set; } = null!;

    /// <summary>
    /// Titre métier du rapport.
    /// Exemple : "Estimated & Check Reads"
    /// </summary>
    public string Topic { get; set; } = null!;

    /// <summary>
    /// Dimension de découpage.
    /// Valeurs possibles : "Class", "EUC + Class", "Reason Code", "Obligation", etc.
    /// NULL si le rapport n'est pas splitté.
    /// </summary>
    public string? SplitBy { get; set; }

    /// <summary>
    /// Format de la métrique principale.
    /// Valeurs : "Percentage", "Count", "Pct/Count", "Count + AQ", etc.
    /// </summary>
    public string? ReportFormat { get; set; }

    /// <summary>
    /// Nombre de mois glissants affichés dans le graphique.
    /// Défaut = 12. Exception : 2A.13 = 2 (mois courant + précédent seulement).
    /// </summary>
    public int RollingMonths { get; set; } = 12;

    /// <summary>
    /// Ordre d'affichage dans le deck PowerPoint / PPTX.
    /// Commence à 1 pour 2A.1, se termine à 22 pour 2A.19.
    /// </summary>
    public int DisplayOrder { get; set; }

    // ── Navigation ──────────────────────────────────────────────────────────
    /// <summary>Définitions des métriques associées à ce rapport.</summary>
    public ICollection<MetricDefinition> Metrics { get; set; } = [];
}
