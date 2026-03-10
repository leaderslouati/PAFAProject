namespace PAFA.Domain.Entities;

/// <summary>
/// Many-to-Many association table between Shipper and ProductClass.
/// Contains monthly portfolio metrics (supply points, total AQ).
/// Composite primary key: ShipperId + ProductClassId + PeriodYear + PeriodMonth.
/// </summary>
public class ShipperProductClass
{
    public Guid ShipperId      { get; set; }
    public int  ProductClassId { get; set; }
    public int  PeriodYear     { get; set; }
    public int  PeriodMonth    { get; set; }

    /// <summary>Number of supply points for this Shipper in this class for this month.</summary>
    public int?     SupplyPointCount { get; set; }

    /// <summary>Total portfolio AQ (MWH) for this month.</summary>
    public decimal? TotalAQ_MWH     { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation ──────────────────────────────────────────────────────
    public Shipper      Shipper      { get; set; } = null!;
    public ProductClass ProductClass { get; set; } = null!;
}