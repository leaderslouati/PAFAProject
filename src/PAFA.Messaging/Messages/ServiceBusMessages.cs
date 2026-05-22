namespace PAFA.Messaging.Messages;

/// <summary>
/// Payload published to the Azure Service Bus "pafa-validation-failure" topic.
/// A downstream consumer (Logic App / Azure Function) reads this and sends the email.
/// </summary>
public sealed record ValidationFailureMessage(
    Guid    IngestionFileId,
    string  FileName,
    string  ReportingPeriod,
    string  SourceSystem,
    int     TotalErrors,
    IReadOnlyList<ValidationErrorPayload> Errors,
    IReadOnlyList<string> Recipients,
    DateTime PublishedAtUtc
);

/// <summary>
/// Payload published to the Azure Service Bus "pafa-ingestion-failure" topic (AC10).
/// </summary>
public sealed record IngestionFailureMessage(
    int     Year,
    int     Month,
    string  TriggerSource,
    string  ErrorMessage,
    int     RetryAttempts,
    DateTime FailedAtUtc,
    IReadOnlyList<string> Recipients
);

/// <summary>
/// Payload published to the Azure Service Bus "pafa-user-welcome" topic.
/// </summary>
public sealed record WelcomeMessage(
    string RecipientEmail,
    string FirstName,
    string TemporaryPassword,
    DateTime PublishedAtUtc
);

/// <summary>A single validation error row inside a Service Bus message.</summary>
public sealed record ValidationErrorPayload(
    int?   RowNumber,
    string ColumnName,
    string ErrorCode,
    string Severity,
    string ErrorMessage,
    string? OriginalValue
);
