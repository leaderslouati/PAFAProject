// ═══════════════════════════════════════════════════════════
// PAFA.Domain/Entities/LookupValue.cs
//
// MANQUANT — À CRÉER
// Table de valeurs de référence multi-catégories.
// Remplace les enums C# trop rigides par une table seedée en BDD.
//
// Catégories utilisées :
//   "ReasonCode"      → Codes AQ 01–09 (2A.8)
//   "AgeBucket"       → a_0_6, b_6_12, c_12_plus (2A.17, 2A.19)
//   "ObligationType"  → MONTHLY_293K, SMART_AMR, ANNUAL (2A.12, 2A.13)
//   "YearBand"        → 1YR, 2YR, 3YR, 4YR (2A.7)
//   "MRECode"         → MRE01026–MRE01030 (2A.6)
//   "PeriodCode"      → P41, P106 (2A.14)
// ═══════════════════════════════════════════════════════════
namespace PAFA.Domain.Entities;

/// <summary>
/// Valeur de référence appartenant à une catégorie de lookup.
/// Permet de gérer sans recompilation les codes, libellés et ordres
/// de tri des dimensions secondaires des métriques.
/// </summary>
public class LookupValue
{
    /// <summary>Identifiant auto-incrémenté. Clé primaire INT.</summary>
    public int LookupId { get; set; }

    /// <summary>
    /// Catégorie du lookup.
    /// Valeurs : "ReasonCode" | "AgeBucket" | "ObligationType" | "YearBand" | "MRECode" | "PeriodCode".
    /// VARCHAR(50), NOT NULL.
    /// </summary>
    public string Category { get; set; } = null!;

    /// <summary>
    /// Code technique de la valeur.
    /// Exemples : "01", "a_0_6", "MONTHLY_293K", "P41", "MRE01026".
    /// VARCHAR(50), NOT NULL. UNIQUE sur (Category, Code).
    /// </summary>
    public string Code { get; set; } = null!;

    /// <summary>
    /// Libellé affiché dans les rapports et l'email de notification.
    /// Exemple : "Confirmed Theft", "a. 0 – 6 months", "Monthly Read – AQ ≥ 293,000 kWh".
    /// VARCHAR(200), NOT NULL.
    /// </summary>
    public string Label { get; set; } = null!;

    /// <summary>
    /// Ordre d'affichage dans les charts et tableaux.
    /// Commence à 1 pour chaque catégorie.
    /// </summary>
    public int SortOrder { get; set; }
   
    // Navigation
    public ICollection<MetricValue> MetricValues { get; set; } = [];
}
