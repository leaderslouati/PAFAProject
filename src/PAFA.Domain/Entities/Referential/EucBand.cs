// ═══════════════════════════════════════════════════════════
// PAFA.Domain/Entities/Referential/EucBand.cs
//
// MANQUANT — À CRÉER
// Table de référence des 9 bandes EUC (Energy Use Category).
// Utilisée par 2A.7 (No Reads), 2A.9 (Standard CF), 2A.10 (Replaced Reads),
// 2B.11a–h (AQ Portfolio).
// Seedée avec 9 lignes fixes (EUC01–EUC09).
// ═══════════════════════════════════════════════════════════
namespace PAFA.Domain.Entities.Referential;

/// <summary>
/// Bande de consommation annuelle (EUC — Energy Use Category).
/// Définit les seuils AQ en kWh qui délimitent chaque bande EUC01 à EUC09.
/// Table de référence statique — 9 lignes fixes, rarement modifiée.
/// </summary>
public class EucBand
{
    /// <summary>
    /// Code de la bande EUC : "EUC01" → "EUC09".
    /// Clé primaire (PK) — VARCHAR(10).
    /// </summary>
    public string EucCode { get; set; } = null!;

    /// <summary>
    /// Description lisible de la plage AQ.
    /// Exemple : "293,000 – 732,000 kWh/yr"
    /// </summary>
    public string Description { get; set; } = null!;

    /// <summary>
    /// Seuil AQ minimum (inclus) en kWh pour appartenir à cette bande.
    /// EUC01 = 0 kWh (pas de borne inférieure).
    /// </summary>
    public long AqThresholdMinKwh { get; set; }

    /// <summary>
    /// Seuil AQ maximum (exclus) en kWh pour cette bande.
    /// EUC09 = long.MaxValue (pas de borne supérieure).
    /// </summary>
    public long AqThresholdMaxKwh { get; set; }
}
