namespace PAFA.Domain.Models;

/// <summary>
/// All data needed to compose and send a validation-failure notification email.
/// </summary>
public sealed record ValidationFailureEmailContext(
    Guid IngestionFileId,
    string FileName,
    string ReportingPeriod,
    string SourceSystem,
    IReadOnlyList<string> Recipients,
    IReadOnlyList<ValidationErrorItem> AllErrors
);

/// <summary>
/// Lightweight representation of a single validation error,
/// used to build the email HTML summary and the full CSV attachment.
/// </summary>
public sealed record ValidationErrorItem(
    int? RowNumber,
    string? ColumnName,
    string ErrorCode,
    string Severity,
    string ErrorMessage,
    string? OriginalValue
);
