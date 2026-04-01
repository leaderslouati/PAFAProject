// ═══════════════════════════════════════════════════════════
// PAFA.Domain/Entities/Referential/ShipperAlias.cs
//
// CHANGEMENTS vs version initiale :
//   ✓ Ajout ValidFrom (DateOnly) — début de validité de l'alias
//   ✓ Ajout ValidTo   (DateOnly?) — fin de validité (NULL = actif)
//   ✓ Ajout IsActive  (bool) — flag calculé, pratique pour les requêtes
//   ✓ Navigation Shipper déjà présente — inchangée
//   ✓ Id passé de int → int (conservé, cohérent avec la référence)
//   POURQUOI : les vues v_parr_industry et vw_dim_shipper utilisent un
//   LEFT JOIN sur ShipperAlias sans contrainte de période. Sans ValidFrom/To,
//   un shipper avec 2 alias retourne 2 lignes dupliquées dans la vue.
// ═══════════════════════════════════════════════════════════
namespace PAFA.Domain.Entities.Referential;

/// <summary>
/// Alias d'anonymisation : mapping SSC ↔ Shipper réel.
/// Utilisé pour les rapports Industry (Schedule 2A) afin de
/// ne pas exposer l'identité réelle des shippers.
/// Un shipper peut avoir plusieurs alias successifs (rotation annuelle).
/// La vue vw_dim_shipper filtre avec ValidFrom/ValidTo pour retourner
/// l'alias actif au moment du reporting.
/// </summary>
public class ShipperAlias : BaseEntity
{
    public int Id { get; set; }

    /// <summary>FK → Shipper.Id</summary>
    public Guid ShipperId { get; set; }

    /// <summary>
    /// Code d'alias anonymisé exposé dans les rapports Industry.
    /// Exemple : "ALPHA-01", "BRAVO-07".
    /// Doit être UNIQUE sur la période de validité.
    /// </summary>
    public string AliasCode { get; set; } = string.Empty;

    /// <summary>
    /// Début de la période de validité de cet alias (inclus).
    /// Typiquement le 1er jour du mois de reporting.
    /// </summary>
    public DateOnly ValidFrom { get; set; }

    /// <summary>
    /// Fin de la période de validité (inclus).
    /// NULL = alias actuellement actif.
    /// Doit être renseigné quand un nouvel alias est attribué au shipper.
    /// </summary>
    public DateOnly? ValidTo { get; set; }

    /// <summary>
    /// Indique si cet alias est l'alias courant du shipper.
    /// Calculé = (ValidTo IS NULL). Mis à jour lors de la rotation.
    /// </summary>
    public bool IsActive { get; set; } = true;

    // ── Navigation ──────────────────────────────────────────────────────────
    public Shipper Shipper { get; set; } = null!;
}