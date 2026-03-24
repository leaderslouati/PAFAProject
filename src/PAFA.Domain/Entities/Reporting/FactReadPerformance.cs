namespace PAFA.Domain.Entities
{
    // PAFA.Domain/Entities/FactReadPerformance.cs
    // Entité keyless — mappe sur la VIEW SQL, pas une table
    public class FactReadPerformance
    {
        public string ReportMonth { get; set; } = string.Empty;
        public string ShipperCode { get; set; } = string.Empty;
        public string? ProductClass { get; set; }
        public decimal? ReadPerfPct { get; set; }
        public decimal? EstimatedPct { get; set; }
        public decimal? CheckReadCount { get; set; }
        public decimal? TotalSites { get; set; }
        public int IsCompliant { get; set; }
    }
}
