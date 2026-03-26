using MediatR;

namespace PAFA.Extraction.Commands.SharePoint;

/// <summary>
/// Déclenche le téléchargement de TOUS les fichiers PARR disponibles
/// depuis la source distante (SharePoint Online), puis les traite un par un.
///
/// La période est déterminée par le chemin du dossier SharePoint ({Année}/{Mois}).
///   - Si fournie (ex: --year 2025 --month 7) → lit /{year}/{month:D2}/
///   - Si absente (null) → lit le dossier du mois UTC courant
///
/// Le nom du fichier n'est jamais utilisé pour déduire la période.
/// </summary>
public sealed record DownloadParrFilesCommand(
    int? Year = null,
    int? Month = null,
    /// <summary>Null = tous les fichiers disponibles dans le dossier période.</summary>
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
