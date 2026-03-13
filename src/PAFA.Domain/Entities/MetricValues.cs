namespace PAFA.Domain.Entities
{
    public class MetricValue : BaseEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid IngestionFileId { get; set; }
        public string ShipperShortCode { get; set; } = string.Empty;
        public string MetricKey { get; set; } = string.Empty; 
        public decimal Value { get; set; }
        public int PeriodYear { get; set; }
        public int PeriodMonth { get; set; }
    }
}