namespace PAFA.Domain.Entities.Referential;

/// <summary>
/// Shipper gazier et son alias d'anonymisation (version aplatie).
/// </summary>
public class Shipper : BaseEntity
{
    public Guid Id { get; set; } 

    /// <summary>Nom officiel du shipper.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Shipper Short Code (SSC) — identifiant unique court.</summary>
    public string ShortCode { get; set; } = string.Empty;

    /// <summary>Alias d'anonymisation exposé dans les rapports Industry.</summary>
    public string AliasCode { get; set; } = string.Empty;

    public string? LegalEntity { get; set; }
    public string? Email { get; set; }
    
    /// <summary>Indique si le shipper est toujours actif sur le marché.</summary>
    public bool IsActive { get; set; } = true;
    
    public DateOnly? MarketEntryDate { get; set; }
    public DateOnly? MarketExitDate { get; set; }
    public int? PortfolioSize { get; set; }

    // ── Navigation (si vous les conservez) ──────────────────────────────────
    public ICollection<ShipperProductClass> ProductClasses { get; set; } = new List<ShipperProductClass>();
    public ICollection<MetricValue> MetricValues { get; set; } = new List<MetricValue>();
}