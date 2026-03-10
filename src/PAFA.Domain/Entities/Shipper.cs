namespace PAFA.Domain.Entities;

/// <summary>
/// Gas shipper active in the UNC market — central entity of the reference data.
/// </summary>
public class Shipper : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Official full name (e.g., "British Gas Trading Ltd").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Shipper Short Code (SSC) — regulatory anonymization identifier for UNC. UNIQUE.</summary>
    public string ShortCode { get; set; } = string.Empty;

    /// <summary>Associated legal entity (may differ from trade name).</summary>
    public string? LegalEntity { get; set; }

    /// <summary>Compliance officer email — encrypted using Always Encrypted in production.</summary>
    public string? ContactEmail { get; set; }

    public string? ContactName { get; set; }

    public bool IsActive { get; set; } = true;

    public DateOnly? MarketEntryDate { get; set; }

    /// <summary>Market exit date. NULL = still active.</summary>
    public DateOnly? MarketExitDate { get; set; }

    /// <summary>Approximate portfolio size (number of supply points).</summary>
    public int? PortfolioSize { get; set; }

    // ── Navigation ──────────────────────────────────────────────────────
    public ICollection<ShipperAlias> Aliases { get; set; } = new List<ShipperAlias>();
    public ICollection<ShipperProductClass> ProductClasses { get; set; } = new List<ShipperProductClass>();
}