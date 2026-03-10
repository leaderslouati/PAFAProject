namespace PAFA.Domain.Entities;

/// <summary>
/// Detailed validation error per file and line.
/// Enables precise diagnostics and partial replay.
/// </summary>
public class ValidationError
{
    public long   Id               { get; set; }

    public Guid   IngestionFileId  { get; set; }

    /// <summary>NULL = global error (file structure).</summary>
    public int?   LineNumber       { get; set; }

    public string? ColumnName      { get; set; }

    /// <summary>Normalized error code (MISSING_VALUE, INVALID_FORMAT, OUT_OF_RANGE).</summary>
    public string ErrorCode        { get; set; } = string.Empty;

    public string ErrorMessage     { get; set; } = string.Empty;

    public string? OriginalValue   { get; set; }

    /// <summary>ERROR | WARNING | INFO.</summary>
    public string Severity         { get; set; } = "ERROR";

    public DateTime CreatedAt      { get; set; } = DateTime.UtcNow;

    // ── Navigation ──────────────────────────────────────────────────────
    public IngestionFile IngestionFile { get; set; } = null!;
}
