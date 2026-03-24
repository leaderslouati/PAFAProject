// ═══════════════════════════════════════════════════════════
// PAFA.Domain/Entities/Referential/Shipper.cs
//
// CHANGEMENTS vs version initiale :
//   ✓ Ajout navigation ShipperAliases (ICollection<ShipperAlias>)
//     nécessaire pour que EF Core gère le ValidFrom/ValidTo
//   ✓ Navigation MetricValues déjà présente — inchangée
//   ✓ Navigation ProductClasses (ShipperProductClass) déjà présente
// ═══════════════════════════════════════════════════════════
namespace PAFA.Domain.Entities.Referential;

/// <summary>
/// Shipper gazier enregistré auprès de l'opérateur réseau.
/// Identifié par un ShortCode unique (SSC) utilisé dans tous
/// les fichiers source CDSP et DDP.
/// Pour les rapports anonymisés (Schedule 2A), l'identité réelle
/// est masquée via ShipperAlias.
/// </summary>
public class Shipper : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Nom officiel du shipper (réel — non exposé en Schedule 2A).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Shipper Short Code (SSC) — identifiant unique court.
    /// Utilisé dans tous les fichiers CDSP/DDP et les MetricValues.
    /// CONTRAINTE : UNIQUE en base.
    /// </summary>
    public string ShortCode { get; set; } = string.Empty;

    public string? LegalEntity { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; } = true;
    public DateOnly? MarketEntryDate { get; set; }
    public DateOnly? MarketExitDate { get; set; }

    /// <summary>Nombre de Supply Points total dans le portefeuille (snapshot).</summary>
    public int? PortfolioSize { get; set; }

    // ── Navigation ──────────────────────────────────────────────────────────
    /// <summary>
    /// Relation many-to-many vers ProductClass via ShipperProductClass.
    /// Contient les métriques mensuelles par PC.
    /// </summary>
    public ICollection<ShipperProductClass> ProductClasses { get; set; }
        = new List<ShipperProductClass>();

    /// <summary>
    /// Toutes les valeurs de métriques EAV pour ce shipper.
    /// Filtrables par MetricKey, ProductClassCode, ReportingPeriod.
    /// </summary>
    public ICollection<MetricValue> MetricValues { get; set; }
        = new List<MetricValue>();

    /// <summary>
    /// Historique complet des alias d'anonymisation.
    /// L'alias actif est celui avec ValidTo IS NULL (IsActive = true).
    /// Utilisé par les vues v_parr_industry et vw_dim_shipper.
    /// </summary>
    public ICollection<ShipperAlias> ShipperAliases { get; set; }
        = new List<ShipperAlias>();
}