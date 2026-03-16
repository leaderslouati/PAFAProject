using PAFA.Domain.Entities;

public class Shipper : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string ShortCode { get; set; } = string.Empty;  // SSC — UNIQUE
    public string? LegalEntity { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; } = true;
    public DateOnly? MarketEntryDate { get; set; }
    public DateOnly? MarketExitDate { get; set; }
    public int? PortfolioSize { get; set; }

    // ── Navigation ───────────────────────────────────────────────
    public ICollection<ShipperProductClass> ProductClasses { get; set; }
        = new List<ShipperProductClass>();

    public ICollection<MetricValue> MetricValues { get; set; }
        = new List<MetricValue>();
}