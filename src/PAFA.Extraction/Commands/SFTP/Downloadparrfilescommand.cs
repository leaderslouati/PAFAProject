using MediatR;

namespace PAFA.Extraction.Commands.Sftp;

/// <summary>
/// Déclenche le téléchargement de TOUS les fichiers PARR disponibles
/// sur le SFTP Xoserve, puis les traite un par un.
///
/// La période est optionnelle :
///   - Si fournie (ex: --year 2025 --month 2) → utilisée pour tous les fichiers
///   - Si absente (null) → détectée automatiquement depuis le nom de chaque fichier
///     Ex: "MOD520A_PAF_Reports_Feb25.xlsx" → 2025-02
/// </summary>
public sealed record DownloadParrFilesCommand(
    int? Year = null,
    int? Month = null,
    /// <summary>Null = tous les fichiers disponibles.</summary>
    List<string>? FileNameFilter = null
) : IRequest<DownloadParrFilesResult>;

public sealed record DownloadParrFilesResult(
    bool Success,
    int FilesDownloaded,
    int FilesImported,
    int FilesFailed,
    List<string> ImportedFiles,
    List<FileError> Errors);

public sealed record FileError(string FileName, string ErrorMessage);
