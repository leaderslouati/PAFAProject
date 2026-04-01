using MediatR;

namespace PAFA.Extraction.Commands.SharePoint;

/// <summary>
/// Query de consultation (dry-run) : liste les fichiers SharePoint disponibles
/// pour la période demandée et identifie ceux non encore traités avec succès.
/// Aucune écriture en base, aucun transfert vers MinIO.
/// 
/// Utilisée par GET /api/sharepoint/pending-files
/// </summary>
public sealed record ListSharePointPendingFilesQuery(
    int Year,
    int Month,
    List<string>? FileNameFilter = null
) : IRequest<ListSharePointPendingFilesResult>;

public sealed record ListSharePointPendingFilesResult(
    bool Success,
    int Year,
    int Month,
    /// <summary>Fichiers présents sur SharePoint et non encore chargés en base.</summary>
    IReadOnlyList<SharePointFileInfo> PendingFiles,
    /// <summary>Fichiers présents sur SharePoint et déjà traités avec succès.</summary>
    IReadOnlyList<SharePointFileInfo> AlreadyLoadedFiles,
    string? ErrorMessage = null
);

public sealed record SharePointFileInfo(
    string FileName,
    string FullRemotePath,
    long SizeBytes,
    DateTime LastModified
);
