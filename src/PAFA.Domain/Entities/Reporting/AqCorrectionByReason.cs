// ═══════════════════════════════════════════════════════════
// PAFA.Domain/Entities/AqCorrectionByReason.cs
//
// MANQUANT — À CRÉER
// Table dédiée pour le rapport 2A.8 "AQ Correction by Reason Code".
// Le modèle EAV (metric_values) ne suffit pas ici car on a besoin
// d'un axe supplémentaire : le ReasonCode (01–09).
// Source : feuille "AQ Correction by Reason Code" de MOD520A.
// ═══════════════════════════════════════════════════════════
using PAFA.Domain.Entities.Referential;

namespace PAFA.Domain.Entities;

/// <summary>
/// Nombre de MPRNs ayant fait l'objet d'une correction AQ par raison et par shipper.
/// Une ligne = 1 shipper × 1 mois × 1 raison de correction.
/// Table distincte de metric_values car la dimension ReasonCode
/// ne peut pas être stockée dans ProductClassCode ou EucCode.
/// </summary>
public class AqCorrectionByReason
{
    /// <summary>Clé primaire UUID.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// FK → Shipper.Id.
    /// NULL pour la ligne "Industry Total" (agrégat toutes shippers).
    /// </summary>
    public Guid? ShipperId { get; set; }

    /// <summary>
    /// Période de reporting au format YYYYMM (ex. 202603 = Mars 2026).
    /// INT NOT NULL.
    /// </summary>
    public int PeriodId { get; set; }

    /// <summary>
    /// FK → LookupValue.LookupId (Category = "ReasonCode").
    /// Fait référence aux codes 01–09 seedés dans lookup_values.
    /// </summary>
    public int ReasonCodeLookupId { get; set; }

    /// <summary>
    /// Nombre de MPRNs du shipper ayant utilisé ce code raison dans le mois.
    /// INT >= 0.
    /// </summary>
    public int MprnCount { get; set; }

    /// <summary>FK → IngestionFile.Id — fichier source de cette valeur.</summary>
    public Guid IngestionFileId { get; set; }

    // ── Navigation ──────────────────────────────────────────────────────────
    public Shipper? Shipper { get; set; }
    public LookupValue ReasonCode { get; set; } = null!;
}
