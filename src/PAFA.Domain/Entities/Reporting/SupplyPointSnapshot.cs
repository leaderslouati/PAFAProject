// ═══════════════════════════════════════════════════════════
// PAFA.Domain/Entities/SupplyPointSnapshot.cs
//
// MANQUANT — À CRÉER
// Table dédiée pour le fichier 202604_Apr2026_SupplyPointCounts.xlsx.
// Grain le plus fin du projet : GasDay × Shipper × Class × EUC × LDZ.
// NE PAS mettre dans metric_values — volume trop grand, grain différent.
// ═══════════════════════════════════════════════════════════
using PAFA.Domain.Entities.Referential;

namespace PAFA.Domain.Entities;

/// <summary>
/// Snapshot journalier du nombre de Supply Points et de l'AQ Rolling
/// par shipper, class produit, bande EUC et zone LDZ.
/// Source : fichier SupplyPointCounts mensuel (feuille "Report 2").
/// Une ligne = 1 Gas Day × 1 Shipper × 1 Class × 1 EUC × 1 LDZ.
/// </summary>
public class SupplyPointSnapshot
{
    /// <summary>Clé primaire UUID.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Date de gaz (Gas Day).
    /// Type DATE en base (pas de composante heure).
    /// Source : colonne "Gas Day" du fichier SupplyPointCounts.
    /// </summary>
    public DateOnly GasDay { get; set; }

    /// <summary>
    /// FK → Shipper.Id.
    /// NULL si la ligne source ne correspond à aucun shipper connu (à investiguer).
    /// </summary>
    public Guid? ShipperId { get; set; }

    /// <summary>
    /// Product Class (1, 2, 3, 4).
    /// Source : colonne "Class" du fichier source.
    /// NULL si non renseigné dans le fichier.
    /// </summary>
    public int? ClassId { get; set; }

    /// <summary>
    /// Code EUC (ex. "EUC01"→"EUC09").
    /// Source : colonne "EUC Identifier Code" — nécessite mapping depuis le code EA:xxx.
    /// VARCHAR(20), NULL si non mappé.
    /// </summary>
    public string? EucCode { get; set; }

    /// <summary>
    /// Code LDZ (Local Distribution Zone).
    /// Source : colonne "Network Location Zone Ldz".
    /// VARCHAR(10), NULL si non renseigné.
    /// </summary>
    public string? LdzCode { get; set; }

    /// <summary>
    /// Nombre de MPRNs (Supply Points) dans ce segment.
    /// INT >= 0. Source : colonne "Count of MPRN".
    /// </summary>
    public int MprnCount { get; set; }

    /// <summary>
    /// AQ Rolling total en kWh pour ce segment.
    /// BIGINT. Source : colonne "AQ_ROLL".
    /// </summary>
    public long AqRoll { get; set; }

    // ── Navigation ──────────────────────────────────────────────────────────
    public Shipper? Shipper { get; set; }
}
