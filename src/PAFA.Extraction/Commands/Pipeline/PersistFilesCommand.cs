using MediatR;

namespace PAFA.Extraction.Commands.Pipeline;

// ── Command ───────────────────────────────────────────────────────────────────

/// <summary>
/// Step 3 — Persist pipeline results to the database and Blob Storage.
/// <list type="bullet">
///   <item>Validated files: blob moved to /processed/, SharePoint → Processed, DB record created.</item>
///   <item>Quarantined files: already in /quarantine/ (moved in Step 2); SharePoint → Quarantined,
///         DB record created with validation errors, Service Bus notification published.</item>
///   <item>After all validated files are persisted: Power BI dataset refresh is triggered.</item>
/// </list>
/// Service Bus notification is always sent regardless of overall pipeline status.
/// </summary>
public sealed record PersistFilesCommand(
    IReadOnlyList<ParseAndValidateResult> ValidationResults,
    int Year,
    int Month,
    Guid CorrelationId
) : IRequest<PersistFilesResult>;

// ── Result ────────────────────────────────────────────────────────────────────

public sealed record PersistFilesResult(
    bool Success,
    PersistenceReport? Report,
    string? ErrorMessage = null);

// ── Models ────────────────────────────────────────────────────────────────────

/// <summary>Summary of the Persist step outcome.</summary>
public sealed record PersistenceReport(
    IReadOnlyList<PersistedFileResult> PersistedFiles,
    bool PowerBiRefreshTriggered);

/// <summary>Result for a single file persisted (or quarantined) in Step 3.</summary>
public sealed record PersistedFileResult(
    string FileName,
    string Status,   // "Persisted" | "Quarantined"
    Guid DbId,
    string BlobPath);
