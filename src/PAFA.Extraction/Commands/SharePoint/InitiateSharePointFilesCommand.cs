using MediatR;

namespace PAFA.Extraction.Commands.SharePoint;

/// <summary>
/// Commande déclenchée HORS fenêtre cron (jours 18–21), via API manuelle.
///
/// Responsabilité : identifier les fichiers SharePoint non encore traités avec succès
/// pour la période demandée, les télécharger, les stocker dans MinIO et créer les
/// enregistrements Job + IngestionFile en base. 
///
/// Après cet appel, le client enchaîne les étapes existantes :
///   POST /api/files/{fileId}/parse
///   POST /api/files/{fileId}/validate
///   POST /api/files/{fileId}/persist
/// </summary>
public sealed record InitiateSharePointFilesCommand(
    int Year,
    int Month,
    /// <summary>
    /// Filtre optionnel sur les noms de fichiers.
    /// Si null ou vide, tous les fichiers pending sont traités.
    /// </summary>
    List<string>? FileNameFilter = null
) : IRequest<InitiateSharePointFilesResult>;

// ?? Result ????????????????????????????????????????????????????????????????????

public sealed record InitiateSharePointFilesResult(
    bool Success,
    int Year,
    int Month,
    /// <summary>Fichiers mis en attente de traitement — un enregistrement par fichier prêt au pipeline.</summary>
    IReadOnlyList<PendingFileEntry> PendingFiles,
    /// <summary>Fichiers détectés sur SharePoint mais ignorés (déjà traités ou erreur de validation du nom).</summary>
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
