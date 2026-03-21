using MediatR;

namespace PAFA.Extraction.Commands.Sftp;

/// <summary>
/// Déclenche le téléchargement des fichiers PARR depuis le SFTP
/// et leur import dans PostgreSQL via le pipeline existant.
/// </summary>
public sealed record DownloadParrFilesCommand(
    int Year,
    int Month,
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
