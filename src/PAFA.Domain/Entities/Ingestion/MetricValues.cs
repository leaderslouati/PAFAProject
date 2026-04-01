// ═══════════════════════════════════════════════════════════
// PAFA.Domain/Entities/MetricValue.cs
//
// CHANGEMENTS vs version initiale :
//   ✓ Ajout de ShipperId (Guid?) — FK directe vers Shipper
//   ✓ Ajout de la navigation Shipper?
//   ✓ Suppression du string ShipperShortCode comme seul lien
//     (gardé pour compatibilité ascendante pendant la migration des données)
//   ✓ Ajout ProductClassCode renommé explicitement (déjà présent, commenté)
//   ✓ Namespace PAFA.Domain.Entities ajouté (manquait dans l'original)
// ═══════════════════════════════════════════════════════════
using PAFA.Domain.Entities.Referential;

namespace PAFA.Domain.Entities;

/// <summary>
/// Valeur d'une métrique mensuelle pour un shipper et une classe produit.
/// Modèle EAV (Entity–Attribute–Value) : chaque ligne = 1 KPI.
/// Exemples de MetricKey : EstimatedPct, CheckReadCount, ReadPerfPct,
///                          TotalSites, NoMeterCount, NoMeterPct.
/// </summary>
public class MetricValue : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateOnly ReportingPeriod { get; set; }

    // ── Lien Shipper (double colonne pendant la période de transition) ──────
    /// <summary>
    /// FK directe vers Shipper.Id.
    /// NULL si la ligne provient d'un ancien fichier non migré.
    /// À rendre NOT NULL après la migration complète des données.
    /// </summary>
    public Guid? ShipperId { get; set; }

    /// <summary>
    /// Code court du shipper (SSC).
    /// Conservé pour compatibilité avec les vues SQL existantes
    /// et les fichiers source CDSP/DDP qui utilisent le SSC.
    /// </summary>
    public string ShipperShortCode { get; set; } = string.Empty;

    // ── Métrique ────────────────────────────────────────────────────────────
    /// <summary>
    /// Clé de la métrique.
    /// Valeurs attendues : EstimatedPct | CheckReadCount | ReadPerfPct |
    ///                     TotalSites   | NoMeterCount   | NoMeterPct.
    /// </summary>
    public string MetricKey { get; set; } = string.Empty;

    /// <summary>Valeur numérique de la métrique (décimal pour tous les types).</summary>
    public decimal Value { get; set; }

    /// <summary>Valeur textuelle optionnelle (observations, statuts, flags).</summary>
    public string? TextValue { get; set; }

    /// <summary>Code de classe produit : PC1 | PC2 | PC3 | PC4. NULL si non applicable.</summary>
    public string? ProductClassCode { get; set; }

    // ── Navigation ──────────────────────────────────────────────────────────
    /// <summary>Fichier d'ingestion source de cette valeur.</summary>
    public Guid IngestionFileId { get; set; }
    public IngestionFile? IngestionFile { get; set; }

    /// <summary>
    /// Navigation vers le shipper réel.
    /// Disponible uniquement si ShipperId est renseigné.
    /// </summary>
    public Shipper? Shipper { get; set; }
}