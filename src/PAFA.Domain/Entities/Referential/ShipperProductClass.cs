// ═══════════════════════════════════════════════════════════
// PAFA.Domain/Entities/Referential/ShipperProductClass.cs
//
// CHANGEMENTS vs version initiale :
//   ✓ Ajout NoMeterCount (int?) — nb de SP sans meter enregistré
//   ✓ Ajout NoMeterPct   (decimal?) — % calculé à l'ingestion
//   ✓ Ajout EstimatedPct (decimal?) — % estimated reads (dénormalisé
//     depuis MetricValues pour éviter le pivot en vue)
//   ✓ Ajout CheckReadCountNotCompleted (int?) — count check reads
//   ✓ Ajout ReadPerfPct (decimal?) — performance lecture globale
//   POURQUOI : ShipperProductClass est la table mensuelle par shipper
//   par PC. Stocker les KPIs ici (en plus de MetricValues) permet à
//   la vue vw_2a2_no_meter de faire un simple SELECT sans pivot EAV.
//   Les vues SQL pour Power BI s'appuient sur cette table.
// ═══════════════════════════════════════════════════════════
namespace PAFA.Domain.Entities.Referential;

/// <summary>
/// Relation mensuelle many-to-many entre Shipper et ProductClass.
/// Contient les métriques agrégées du portefeuille du shipper
/// pour ce mois et cette classe produit.
/// Une ligne = 1 shipper × 1 product class × 1 mois.
/// </summary>
public class ShipperProductClass : BaseEntity
{
    // ── Clé composite (ShipperId + ProductClassId + ReportingPeriod) ────────
    public Guid ShipperId { get; set; }
    public int ProductClassId { get; set; }
    public DateOnly ReportingPeriod { get; set; }

    // ── Métriques portefeuille ───────────────────────────────────────────────
    /// <summary>
    /// Nombre total de points de fourniture (Supply Points) du shipper
    /// dans cette classe produit pour ce mois.
    /// </summary>
    public int? SupplyPointCount { get; set; }

    /// <summary>Quantité annuelle totale du portefeuille (MWH).</summary>
    public decimal? TotalAQ_MWH { get; set; }

    // ── Métriques lecture (US2 — Estimated & Check Reads) ───────────────────
    /// <summary>
    /// Pourcentage de lectures estimées (0–100).
    /// Source : CDSP/DDP fichier mensuel, MetricKey = 'EstimatedPct'.
    /// Dénormalisé ici pour simplifier les vues Power BI.
    /// </summary>
    public decimal? EstimatedPct { get; set; }

    /// <summary>
    /// Nombre de check reads non complétés pour ce mois.
    /// Source : MetricKey = 'CheckReadCount'. Entier >= 0.
    /// </summary>
    public int? CheckReadCountNotCompleted { get; set; }

    /// <summary>
    /// Pourcentage global de performance de lecture (0–100).
    /// Utilisé pour le calcul is_compliant vs UNC threshold (97.5% pour PC1).
    /// Source : MetricKey = 'ReadPerfPct'.
    /// </summary>
    public decimal? ReadPerfPct { get; set; }

    // ── Métriques No Meter (US3 — No Meter Recorded) ────────────────────────
    /// <summary>
    /// Nombre de Supply Points sans meter enregistré dans le registre SP.
    /// Source : CDSP, MetricKey = 'NoMeterCount'. Entier >= 0.
    /// </summary>
    public int? NoMeterCount { get; set; }

    /// <summary>
    /// Pourcentage de SP sans meter = NoMeterCount / SupplyPointCount × 100.
    /// Calculé à l'ingestion et stocké pour éviter la division en vue SQL.
    /// PC1 et PC2 = toujours 0 par conception (obligation réglementaire).
    /// </summary>
    public decimal? NoMeterPct { get; set; }

    // ── Navigation ──────────────────────────────────────────────────────────
    public Shipper Shipper { get; set; } = null!;
    public ProductClass ProductClass { get; set; } = null!;
}