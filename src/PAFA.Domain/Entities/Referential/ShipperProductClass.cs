namespace PAFA.Domain.Entities.Referential;

/// <summary>
/// Many-to-many between Shipper and ProductClass.
/// Contains monthly portfolio metrics per shipper per product class.
/// </summary>
public class ShipperProductClass : BaseEntity
{
    public Guid ShipperId { get; set; }
    public int ProductClassId { get; set; }
    public DateOnly ReportingPeriod { get; set; }

    /// <summary>Number of supply points for this shipper in this class.</summary>
    public int? SupplyPointCount { get; set; }

    /// <summary>Total portfolio Annual Quantity (MWH) for this month.</summary>
    public decimal? TotalAQ_MWH { get; set; }

    // ── Navigation ─────────────────────────────────────────────────
    public Shipper Shipper { get; set; } = null!;
    public ProductClass ProductClass { get; set; } = null!;
}