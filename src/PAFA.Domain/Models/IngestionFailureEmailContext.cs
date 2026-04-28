namespace PAFA.Domain.Models;

/// <summary>
/// Context for sending an ingestion failure notification after all retries are exhausted (AC10).
/// </summary>
public sealed record IngestionFailureEmailContext(
    int Year,
    int Month,
    string TriggerSource,
    string ErrorMessage,
    int RetryAttempts,
    DateTime FailedAtUtc,
    IReadOnlyList<string> Recipients
);
