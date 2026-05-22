using MediatR;

namespace PAFA.Extraction.Commands.SharePoint;

/// <summary>
/// Commande déclenchée via POST /api/sharepoint/start.
///
/// Year et Month sont optionnels : si absents, le mois et l'année courants sont utilisés.
///
/// Flux de découverte des fichiers :
///   1. Lister /{BaseInboundPath}/{YYYY}/{MM}/   ? fichiers neufs à traiter
///   2. Si ReprocessFailed=true, lister /processed/{YYYY}/{MM}/ et /failed/{YYYY}/{MM}/
///      ? déplacer chaque fichier trouvé vers /inbound avant de le traiter
/// </summary>
public sealed record InitiateSharePointFilesCommand(
    /// <summary>Année de la période. Si null, utilise l'année courante.</summary>
    int? Year = null,
    /// <summary>Mois de la période (1-12). Si null, utilise le mois courant.</summary>
    int? Month = null,
    /// <summary>Filtre optionnel sur les noms de fichiers.</summary>
    List<string>? FileNameFilter = null,
    /// <summary>
    /// Si true, les fichiers présents dans /processed/{YYYY}/{MM} ou /failed/{YYYY}/{MM}
    /// sont déplacés vers /inbound et retraités. Défaut : false.
    /// </summary>
    bool ReprocessFailed = false
) : IRequest<InitiateSharePointFilesResult>;

// ?? Result ????????????????????????????????????????????????????????????????????

public sealed record InitiateSharePointFilesResult(
    bool Success,
    int Year,
    int Month,
    /// <summary>Fichiers mis en attente de traitement.</summary>
    IReadOnlyList<PendingFileEntry> PendingFiles,
    /// <summary>Fichiers ignorés (déjà traités ou erreur de validation du nom).</summary>
    IReadOnlyList<SkippedFileRecord> SkippedFiles,
    string? ErrorMessage = null
);

/// <summary>
/// Représente un fichier prêt à être parsé/validé/persisté.
/// Le client utilise <see cref="FileId"/> pour les appels parse ? validate ? persist.
/// </summary>
public sealed record PendingFileEntry(
    Guid JobId,
    Guid FileId,
    string FileName,
    string BlobPath,
    long SizeBytes
);
