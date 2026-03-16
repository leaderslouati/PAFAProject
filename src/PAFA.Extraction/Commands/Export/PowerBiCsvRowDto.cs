namespace PAFA.Extraction.Commands.Export; 
public record PowerBiCsvRowDto
{
   public DateOnly PeriodeDate { get; set; }
    public string ShipperCode { get; init; } = string.Empty;
    public int? ProductClass { get; init; }
    public string? MrfCode { get; init; }
    public decimal? ReadPerformancePct { get; init; }
    public decimal? EstimatedReadPct { get; init; }
    public int? AqOverdueCount { get; init; }
    public int? TotalSiteCount { get; init; }
    public bool IsIndustryAverage { get; init; }
}
