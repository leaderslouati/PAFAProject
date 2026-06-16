// ═══════════════════════════════════════════════════════════════════════════════
// PAFA.Domain/Entities/MetricValue.cs
//
// ENTITÉ PRINCIPALE — à créer dans PAFA.Domain/Entities/
// Stocke chaque valeur de métrique issue de l'ingestion des fichiers PARR.
//
// Structure issue de l'analyse des fichiers réels :
//   MOD520A__PAF_Reports_Apr26_Anonymised.xlsx  → 2A.1 (anonymisé)
//   MOD520A__PAF_Reports_Apr26_Non_Anonymised.xlsx → 2B.1 (non-anonymisé)
//
// Grain : 1 ligne = 1 shipper × 1 période × 1 metric_key × 1 product_class
// ═══════════════════════════════════════════════════════════════════════════════

using PAFA.Domain.Entities.Referential;

namespace PAFA.Domain.Entities;

/// <summary>
/// Représente une valeur de métrique PARR persistée après ingestion d'un fichier Excel.
///
/// Exemple 2A.1 :
///   ShipperShortCode = "Gitega"   (alias anonymisé)
///   MetricKey        = "EST_PCT"
///   Value            = 0.0162     (1.62%)
///   ProductClassCode = "PC1"
///   ReportCode       = "2A.1"
///   ReportingPeriod  = 2026-03-01
///
/// Exemple 2B.1 :
///   ShipperShortCode = "NGS"      (short code réel)
///   ShipperId        = <guid>     (FK vers shippers)
///   MetricKey        = "EST_PCT"
///   Value            = 0.0162
///   ProductClassCode = "PC1"
///   ReportCode       = "2A.1"     (même code — 2B.1 = version non-anon de 2A.1)
/// </summary>
public class MetricValue : BaseEntity
{
    // ── Identity ─────────────────────────────────────────────────────────────
    public Guid Id { get; set; } = Guid.NewGuid();

    // ── Période de reporting ──────────────────────────────────────────────────
    /// <summary>
    /// Premier jour du mois de reporting (DateOnly → date PostgreSQL).
    /// Exemple : 2026-03-01 pour Mars 2026.
    /// </summary>
    public DateOnly ReportingPeriod { get; set; }

    // ── Identification du Shipper ─────────────────────────────────────────────
    /// <summary>
    /// FK nullable vers la table shippers.
    /// NULL pour les lignes anonymisées (2A) — on n'a que le label.
    /// Renseigné pour les lignes non-anonymisées (2B).
    /// </summary>
    public Guid? ShipperId { get; set; }

    /// <summary>
    /// Code court du shipper, commun aux deux formats.
    /// - Pour 2A (anonymisé)     : alias capital de ville (ex: "Gitega", "Rome")
    /// - Pour 2B (non-anonymisé) : code réel CDSP (ex: "NGS", "AGA", "BRK")
    /// Toujours renseigné. Sert de clé de jointure dans les vues SQL.
    /// </summary>
    public string ShipperShortCode { get; set; } = string.Empty;

    // ── Métrique ──────────────────────────────────────────────────────────────
    /// <summary>
    /// Clé normalisée de la métrique. Exemples :
    ///   "EST_PCT"        → % Estimated Reads   (valeur 0-1 ou 0-100)
    ///   "CHECK_COUNT"    → Count of Check Reads not provided (entier)
    ///   "READ_PERF_PCT"  → % Read Performance
    /// Voir MetricValueMapper pour la table de correspondance complète.
    /// </summary>
    public string MetricKey { get; set; } = string.Empty;

    /// <summary>
    /// Valeur numérique de la métrique.
    /// Convention : les % sont stockés en fraction décimale (0.162 = 16.2%).
    /// Les counts sont stockés tels quels (ex: 19).
    /// Precision : NUMERIC(18,6).
    /// </summary>
    public decimal Value { get; set; }

    /// <summary>
    /// Valeur textuelle optionnelle pour les cas non-numériques.
    /// Exemple : "-" présent dans certaines cellules du fichier source.
    /// </summary>
    public string? TextValue { get; set; }

    // ── Dimensions additionnelles ─────────────────────────────────────────────
    /// <summary>
    /// Code de la classe produit concernée.
    /// Valeurs : "PC1", "PC2", "PC3", "PC4", null (si valeur agrégée toutes classes).
    /// Issu de la section du fichier Excel (PC1 rows 9-32, PC2 rows 34-52, etc.)
    /// </summary>
    public string? ProductClassCode { get; set; }

    /// <summary>
    /// Code du rapport PARR dont cette métrique est issue.
    /// FK nullable vers report_definitions.ReportCode.
    /// Exemples : "2A.1", "2A.5", "2B.11a".
    /// NULL pour les métriques héritées avant la migration.
    /// </summary>
    public string? ReportCode { get; set; }

    /// <summary>
    /// Code de la bande EUC (Energy Use Category) pour les métriques dimensionnées.
    /// FK nullable vers euc_bands.EucCode.
    /// Renseigné pour : 2A.7 (No Reads), 2A.9 (Standard CF), 2A.10 (Replaced Reads).
    /// NULL pour 2A.1, 2A.5, etc.
    /// </summary>
    public string? EucCode { get; set; }

    /// <summary>
    /// FK nullable vers lookup_values.LookupId.
    /// Renseigné pour les métriques avec dimension secondaire :
    ///   - AgeBucket      : 2A.17, 2A.19
    ///   - ObligationType : 2A.12a/b/c, 2A.13
    ///   - YearBand       : 2A.7
    ///   - MRECode        : 2A.6
    ///   - PeriodCode     : 2A.14
    ///   - ReasonCode     : 2A.8
    /// </summary>
    public int? LookupValueId { get; set; }

    // ── Source de la donnée ───────────────────────────────────────────────────
    /// <summary>
    /// FK vers ingestion_files.Id — obligatoire.
    /// Tracabilité : permet de savoir quel fichier Excel a produit cette ligne.
    /// </summary>
    public Guid IngestionFileId { get; set; }

    // ── Soft delete & concurrence ─────────────────────────────────────────────
    public bool IsDeleted { get; set; } = false;
    public byte[]? RowVersion { get; set; }

    // ── Navigation properties ─────────────────────────────────────────────────
    public Shipper? Shipper { get; set; }
    public IngestionFile? IngestionFile { get; set; }
    public LookupValue? LookupValue { get; set; }
    public ReportDefinition? Report { get; set; }
}