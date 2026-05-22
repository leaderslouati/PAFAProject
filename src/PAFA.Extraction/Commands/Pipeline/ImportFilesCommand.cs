using MediatR;
using PAFA.Domain.Enums;

namespace PAFA.Extraction.Commands.Pipeline;

// ── Command ───────────────────────────────────────────────────────────────────

/// <summary>
/// Step 1 — Import files from SharePoint for the given period.
/// Lists files in the {Year}/{Month} folder, validates names and folder structure,
/// uploads valid files to Blob Storage /inbound/, and locks them in SharePoint
/// (ProcessingStatus = Processing) to prevent duplicate processing.
/// </summary>
public sealed record ImportFilesCommand(
    int Year,
    int Month,
    Guid CorrelationId
) : IRequest<ImportFilesResult>;

// ── Result ────────────────────────────────────────────────────────────────────

public sealed record ImportFilesResult(
    bool Success,
    IReadOnlyList<ImportedFile> Files,
    string? ErrorMessage = null);

// ── Models ────────────────────────────────────────────────────────────────────

/// <summary>Outcome of a single file processed during the Import step.</summary>
public sealed record ImportedFile(
    string FileName,
    string BlobPath,
    ImportStatus Status,
    string? SkipReason);
