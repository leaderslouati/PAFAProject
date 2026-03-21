namespace PAFA.Reports.Batch.Models;

/// <summary>
/// Context for a single report generation operation.
/// Immutable data passed to ReportGenerator implementations.
/// </summary>
public sealed record ReportGenerationContext
{
    public required int Year { get; init; }
    public required int Month { get; init; }
    public required string OutputDirectory { get; init; }
    public required string TempDirectory { get; init; }
    public string? ShipperCode { get; init; }
    public string? ReportType { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = new();

    public DateOnly ReportingPeriod => new(Year, Month, 1);
    
    public string GetContextDescription() 
        => $"{Year:D4}-{Month:D2}" 
           + (string.IsNullOrEmpty(ShipperCode) ? "" : $"_{ShipperCode}")
           + (string.IsNullOrEmpty(ReportType) ? "" : $"_{ReportType}");
}
