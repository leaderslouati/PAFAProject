// ═══════════════════════════════════════════════════════════
// PAFA.Domain/Entities/Report.cs
//
// CHANGEMENTS vs version initiale :
//   ✓ Ajout ObservationsText (string?) — observations mensuelles PAFA
//   ✓ Ajout ObservationsUpdatedAt (DateTime?) — audit de la mise à jour
//   ✓ Ajout ObservationsBy (string?) — qui a saisi les observations
//   ✓ Ajout IngestionJobId (Guid?) — lien optionnel vers le job source
//   POURQUOI : les 3 slides (US1, US2, US3) ont une section "Observations"
//   saisie manuellement par l'analyste PAFA. Stockée en base plutôt qu'en
//   textbox PowerPoint pour permettre la persistence entre sessions et
//   l'historisation par mois de reporting.
// ═══════════════════════════════════════════════════════════
using PAFA.Domain.Enums;

namespace PAFA.Domain.Entities;

/// <summary>
/// Instance mensuelle d'un rapport PARR généré par le système.
/// Référence les fichiers PDF/Excel/PPTX stockés sur Azure Blob Storage.
/// La section Observations est saisie manuellement par l'analyste PAFA
/// et lue depuis la base par Power BI via une table connectée.
/// </summary>
public class Report : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>FK → ReportType (SCH2A ou SCH2B).</summary>
    public int ReportTypeId { get; set; }

    /// <summary>Numéro du rapport dans le schedule (1 à 22).</summary>
    public int ScheduleNumber { get; set; }

    /// <summary>Titre complet, ex. "2A.1 Estimated and Check Reads – Products 1 and 2".</summary>
    public string Title { get; set; } = string.Empty;

    public DateOnly ReportingPeriod { get; set; }

    /// <summary>
    /// Audience cible pour cette instance de rapport.
    /// Dérivé du ReportType mais stocké explicitement pour la performance.
    /// Industry = anonymisé (2A). PAC = non-anonymisé (2B).
    /// </summary>
    public ReportAudience Audience { get; set; } = ReportAudience.Industry;

    public ReportStatus Status { get; set; } = ReportStatus.Pending;

    public DateTime? GeneratedAt { get; set; }
    public DateTime? PublishedAt { get; set; }

    // ── Chemins Azure Blob Storage ──────────────────────────────────────────
    /// <summary>Chemin Azure Blob vers le fichier PDF généré.</summary>
    public string? FilePath_PDF { get; set; }

    /// <summary>Chemin Azure Blob vers le fichier Excel généré.</summary>
    public string? FilePath_Excel { get; set; }

    /// <summary>Chemin Azure Blob vers le dashboard PowerPoint (optionnel).</summary>
    public string? FilePath_PPTX { get; set; }

    // ── Commentaire analytique (existant) ───────────────────────────────────
    /// <summary>Commentaire éditorial de l'analyste PAFA pour ce rapport.</summary>
    public string? CommentaryText { get; set; }
    public string? CommentaryBy { get; set; }

    // ── Observations mensuelles (nouveau — US1, US2, US3) ───────────────────
    /// <summary>
    /// Texte des observations mensuelles saisies manuellement par l'analyste PAFA.
    /// Affiché dans la section "Observations" de chaque slide PowerPoint.
    /// Peut contenir plusieurs bullet points séparés par des sauts de ligne.
    /// Exemple :
    ///   "- The number of uncompleted check reads in PC2 has shown a slight decrease.\n
    ///    - PC1 estimated reads increased slightly in February."
    /// </summary>
    public string? ObservationsText { get; set; }

    /// <summary>Identifiant de l'analyste ayant saisi les observations.</summary>
    public string? ObservationsBy { get; set; }

    /// <summary>Horodatage UTC de la dernière mise à jour des observations.</summary>
    public DateTime? ObservationsUpdatedAt { get; set; }

    // ── Lien ingestion ───────────────────────────────────────────────────────
    /// <summary>
    /// FK optionnelle vers le job d'ingestion qui a déclenché la génération.
    /// NULL si le rapport a été généré manuellement (ad hoc).
    /// </summary>
    public Guid? IngestionJobId { get; set; }

    /// <summary>True = rapport de référence utilisé pour les comparaisons YoY.</summary>
    public bool IsBaseline { get; set; } = false;

    // ── Navigation ──────────────────────────────────────────────────────────
    public ReportType ReportType { get; set; } = null!;
    public IngestionJob? IngestionJob { get; set; }
}