using MediatR;
using PAFA.Extraction.Validation;

namespace PAFA.Extraction.Commands.Import;

/// <summary>
/// Applies all business validation rules (VAL-002..VAL-013) on the
/// previously parsed rows and persists ValidationError records.
/// The IngestionFile status is updated based on findings.
/// </summary>
public record ValidateFileCommand(Guid FileId) : IRequest<ValidateFileResult>;

public record ValidateFileResult(
    bool Success,
    Guid FileId,
    bool HasBlockingErrors,
    int ValidRowCount,
    int InvalidRowCount,
    string? ErrorMessage,
    IReadOnlyList<ValidationFinding>? Findings = null
);
