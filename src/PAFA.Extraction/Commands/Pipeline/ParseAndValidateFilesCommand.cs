using MediatR;
using PAFA.Domain.Enums;

namespace PAFA.Extraction.Commands.Pipeline;

// ── Command ───────────────────────────────────────────────────────────────────

/// <summary>
/// Step 2 — Parse and validate each imported file against the six pipeline rules.
/// Files that fail validation are moved to Blob /quarantine/ and a read-only link is generated.
/// Atomicity: no partial writes — a failed file goes entirely to quarantine.
/// </summary>
public sealed record ParseAndValidateFilesCommand(
    IReadOnlyList<ImportedFile> ImportedFiles,
    int Year,
    int Month,
    Guid CorrelationId
) : IRequest<ParseAndValidateFilesResult>;

// ── Result ────────────────────────────────────────────────────────────────────

public sealed record ParseAndValidateFilesResult(
    bool Success,
    IReadOnlyList<ParseAndValidateResult> Files,
    string? ErrorMessage = null);

// ── Models ────────────────────────────────────────────────────────────────────

/// <summary>Outcome of parsing + validating a single file (Step 2).</summary>
public sealed record ParseAndValidateResult(
    string FileName,
    string BlobPath,
    ValidationStatus Status,
    IReadOnlyList<PipelineValidationError> Errors,
    string? QuarantineBlobPath,
    string? QuarantineFolderLink);

/// <summary>A single rule violation found during Step 2 validation.</summary>
public sealed record PipelineValidationError(
    string RuleName,
    IReadOnlyList<ValidationExample> Examples);

/// <summary>An individual example (row + value) illustrating a rule violation.</summary>
public sealed record ValidationExample(
    int RowNumber,
    string Value);
