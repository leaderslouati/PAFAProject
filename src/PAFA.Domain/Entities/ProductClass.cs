namespace PAFA.Domain.Entities;

/// <summary>
/// UNC gas product class (PC1 to PC4).
/// Defines AQ thresholds and minimum read percentage required per class.
/// Reference table — rarely modified.
/// </summary>
public class ProductClass
{
    public int     Id                  { get; set; }

    /// <summary>Regulatory code: PC1, PC2, PC3, PC4.</summary>
    public string  Code                { get; set; } = string.Empty;

    public string  Description         { get; set; } = string.Empty;

    /// <summary>Low AQ threshold (Annual Quantity, MWH) defining class entry.</summary>
    public decimal? AQThresholdLow     { get; set; }

    /// <summary>High AQ threshold — NULL for PC1 (no upper bound).</summary>
    public decimal? AQThresholdHigh    { get; set; }

    /// <summary>Minimum read percentage required (e.g., 97.5 for PC1).</summary>
    public decimal? MinReadPercentage  { get; set; }

    public bool    IsActive            { get; set; } = true;

    // ── Navigation ──────────────────────────────────────────────────────
    public ICollection<ShipperProductClass>  ShipperProductClasses { get; set; } = new List<ShipperProductClass>();
}