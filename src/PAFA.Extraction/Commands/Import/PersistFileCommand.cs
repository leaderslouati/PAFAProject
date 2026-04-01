using MediatR;

namespace PAFA.Extraction.Commands.Import;

/// <summary>
/// Persists MetricValues for a validated file, then moves the blob:
///   → "processed/{year}/{month}/{fileName}"  on success
///   → "failed/{year}/{month}/{fileName}"     on failure / blocking errors
/// Updates IngestionFile and IngestionJob final statuses.
/// </summary>
public record PersistFileCommand(Guid FileId) : IRequest<PersistFileResult>;

public record PersistFileResult(
    bool Success,
    Guid FileId,
    int MetricsInserted,
    string? FinalBlobPath,
    string? ErrorMessage
);
