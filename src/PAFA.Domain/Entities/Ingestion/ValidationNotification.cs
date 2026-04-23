namespace PAFA.Domain.Entities;

/// <summary>
/// Audit record of every validation-failure notification that has been dispatched.
/// Satisfies the "system must log the failure event and notification dispatch" requirement.
/// </summary>
public class ValidationNotification : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>FK ? IngestionFile that triggered this notification.</summary>
    public Guid IngestionFileId { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string ReportingPeriod { get; set; } = string.Empty;
    public string SourceSystem { get; set; } = string.Empty;

    /// <summary>Semicolon-separated list of recipient e-mail addresses.</summary>
    public string Recipients { get; set; } = string.Empty;

    /// <summary>Total number of validation errors in the file.</summary>
    public int TotalErrors { get; set; }

    /// <summary>UTC timestamp when the email was dispatched.</summary>
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    /// <summary>SENT | FAILED</summary>
    public string Status { get; set; } = "SENT";

    /// <summary>Non-null when Status = FAILED — records the exception message.</summary>
    public string? ErrorDetail { get; set; }

    // ?? Navigation ?????????????????????????????????????????????????
    public IngestionFile IngestionFile { get; set; } = null!;
}
