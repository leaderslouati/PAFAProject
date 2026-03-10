namespace PAFA.Domain.Entities;

/// <summary>
/// Anonymization alias for a Shipper used in Industry reports (Schedule 2A).
/// Enables rotation of anonymized codes while maintaining internal traceability.
/// </summary>
public class ShipperAlias
{
    public int     Id        { get; set; }

    public Guid    ShipperId { get; set; }

    /// <summary>Anonymized code visible to industry (e.g., "Shipper_A", "SSC_007"). UNIQUE.</summary>
    public string  AliasCode { get; set; } = string.Empty;

    public DateOnly ValidFrom { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>NULL = alias still active.</summary>
    public DateOnly? ValidTo  { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string   CreatedBy { get; set; } = "SYSTEM";

    // ── Navigation ──────────────────────────────────────────────────────
    public Shipper Shipper { get; set; } = null!;
}