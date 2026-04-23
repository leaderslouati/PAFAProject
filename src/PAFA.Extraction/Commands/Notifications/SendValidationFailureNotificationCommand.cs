using MediatR;
using PAFA.Domain.Models;

namespace PAFA.Extraction.Commands.Notifications;

/// <summary>
/// Command dispatched after validation detects blocking errors.
/// Triggers the failure notification email + audit log entry.
/// </summary>
public record SendValidationFailureNotificationCommand(
    Guid IngestionFileId,
    string FileName,
    string ReportingPeriod,
    string SourceSystem,
    IReadOnlyList<ValidationErrorItem> AllErrors
) : IRequest<SendValidationFailureNotificationResult>;

public record SendValidationFailureNotificationResult(
    bool Success,
    string? ErrorMessage = null
);
