using MediatR;
using PAFA.Domain.Interfaces;

namespace PAFA.Extraction.Commands.Import;

/// <summary>
/// Parses the raw file from blob storage and stores the parsed rows
/// in memory for the next pipeline step (ValidateFileCommand).
/// The IngestionFile status is updated to "Validating".
/// </summary>
public record ParseFileCommand(Guid FileId) : IRequest<ParseFileResult>;

public record ParseFileResult(
    bool Success,
    Guid FileId,
    int TotalRows,
    string? ErrorMessage,
    /// <summary>Parsed rows forwarded to the validation step.</summary>
    IReadOnlyList<RawDataRow>? Rows = null
);
