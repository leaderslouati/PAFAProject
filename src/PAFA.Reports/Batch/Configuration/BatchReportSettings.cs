namespace PAFA.Reports.Batch.Configuration;

/// <summary>
/// Configuration for batch report generation.
/// Read from appsettings.json or environment variables.
/// </summary>
public class BatchReportSettings
{
    public const string SectionName = "BatchReports";

    /// <summary>Target year for report generation (e.g., 2025).</summary>
    public int TargetYear { get; set; }

    /// <summary>Target month for report generation (1-12).</summary>
    public int TargetMonth { get; set; }

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
