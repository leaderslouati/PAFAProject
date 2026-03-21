// PAFA.Domain/Entities/MetricValue.cs
using PAFA.Domain.Entities;

public class MetricValue : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateOnly ReportingPeriod { get; set; }
    public string ShipperShortCode { get; set; } = string.Empty;
    public string MetricKey { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string? TextValue { get; set; }
    public string? ProductClassCode { get; set; }

    // Navigation existantes — ne pas toucher
    public IngestionFile? IngestionFile { get; set; }
    public Guid IngestionFileId { get; set; }
}