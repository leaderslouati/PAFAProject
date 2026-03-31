using MediatR;
using PAFA.Domain.Enums;

namespace PAFA.Extraction.Commands.SharePoint;

/// <summary>
/// Déclenche le téléchargement de TOUS les fichiers PARR disponibles
/// depuis la source distante (SharePoint Online), puis les traite un par un.
///
/// TriggerSource values:
///   "CRON_AUTO"         — fired by the hosted background service inside the cron window.
///   "MANUAL_API"        — fired by a user via POST /api/ingest outside the cron window.
///   "MANUAL_REPROCESS"  — fired by a user via POST /api/ingest/reprocess after corrections.
/// </summary>
public sealed record DownloadParrFilesCommand(
    int? Year = null,
    int? Month = null,
    /// <summary>Null = tous les fichiers disponibles dans le dossier période.</summary>
    List<string>? FileNameFilter = null,
    /// <summary>Origin of the trigger — see summary for allowed values.</summary>
    string TriggerSource = "MANUAL_API",
    /// <summary>Resolved from the schedule service — Automatic inside window, Manual outside.</summary>
    TriggerMode TriggerMode = TriggerMode.Manual
) : IRequest<DownloadParrFilesResult>;

public sealed record DownloadParrFilesResult(
    bool Success,
    int FilesDownloaded,
    int FilesImported,
    int FilesFailed,
    List<string> ImportedFiles,
    List<FileError> Errors,
    List<SkippedFileRecord> SkippedFiles,
    string TriggerSource,
    string TriggerMode);

public sealed record FileError(string FileName, string ErrorMessage);

/// <summary>
/// Records a file that was detected in the inbound folder but skipped
/// before any download or processing attempt.
/// RuleId maps to FOLD-001, FOLD-002, NAME-001 … NAME-004.
/// </summary>
public sealed record SkippedFileRecord(
    string FileName,
    string RuleId,
    string Reason,
    DateTime SkippedAt);
