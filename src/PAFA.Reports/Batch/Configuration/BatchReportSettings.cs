namespace PAFA.Reports.Batch.Configuration;

/// <summary>
/// Configuration for batch report generation.
/// Read from appsettings.json or environment variables.
/// </summary>
public class BatchReportSettings
{
    public const string SectionName = "BatchReports";

    /// <summary>Target year for report generation. Defaults to current UTC year if not set.</summary>
    public int TargetYear { get; set; } = DateTime.UtcNow.Year;

    /// <summary>Target month for report generation (1-12). Defaults to current UTC month if not set.</summary>
    public int TargetMonth { get; set; } = DateTime.UtcNow.Month;

    /// <summary>Output directory for generated reports (e.g., /var/reports).</summary>
    public string OutputDirectory { get; set; } = "./reports";

    /// <summary>Temporary directory for .tmp files.</summary>
    public string TempDirectory { get; set; } = "./reports/temp";

    /// <summary>Enable/disable PDF generation.</summary>
    public bool GeneratePdf { get; set; } = true;

    /// <summary>Enable/disable Excel generation.</summary>
    public bool GenerateExcel { get; set; } = true;

    /// <summary>Enable/disable CSV generation.</summary>
    public bool GenerateCsv { get; set; } = true;

    /// <summary>Maximum retry attempts on transient errors.</summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>List of shipper codes to process. If empty, process all.</summary>
    public List<string> ShipperCodes { get; set; } = new();

    /// <summary>List of report types to generate (SCH2A, SCH2B). If empty, generate all.</summary>
    public List<string> ReportTypes { get; set; } = new();
}
