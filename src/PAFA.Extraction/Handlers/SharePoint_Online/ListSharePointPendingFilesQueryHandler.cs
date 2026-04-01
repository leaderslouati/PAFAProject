using MediatR;
using Microsoft.Extensions.Logging;
using PAFA.Domain.Interfaces;
using PAFA.Domain.Repositories;
using PAFA.Extraction.Commands.SharePoint;
using PAFA.Extraction.Helpers;
using PAFA.Extraction.Validations;

namespace PAFA.Extraction.Handlers.SharePoint_Online;

/// <summary>
/// Handler pour <see cref="ListSharePointPendingFilesQuery"/>.
///
/// Lecture seule — aucune écriture en base, aucun transfert vers MinIO.
/// Retourne la liste des fichiers SharePoint partitionnée en :
///   - PendingFiles    : fichiers à traiter (pas encore Loaded en base)
///   - AlreadyLoadedFiles : fichiers déjà traités avec succès
/// </summary>
public sealed class ListSharePointPendingFilesQueryHandler
    : IRequestHandler<ListSharePointPendingFilesQuery, ListSharePointPendingFilesResult>
{
    private readonly IRemoteFileSource _fileSource;
    private readonly IUnitOfWork _uow;
    private readonly IFileSourceSettings _settings;
    private readonly ISharePointFileHelper _helper;
    private readonly ILogger<ListSharePointPendingFilesQueryHandler> _log;

    public ListSharePointPendingFilesQueryHandler(
        IRemoteFileSource fileSource,
        IUnitOfWork uow,
        IFileSourceSettings settings,
        ISharePointFileHelper helper,
        ILogger<ListSharePointPendingFilesQueryHandler> log)
    {
        _fileSource = fileSource;
        _uow = uow;
        _settings = settings;
        _helper = helper;
        _log = log;
    }

    public async Task<ListSharePointPendingFilesResult> Handle(
        ListSharePointPendingFilesQuery query, CancellationToken ct)
    {
        var (year, month) = (query.Year, query.Month);
        _log.LogInformation("[SP_LIST] Consultation — Période: {Year}-{Month:D2}", year, month);

        // ?? 1. Test connexion ?????????????????????????????????????????????
        if (!await _fileSource.TestConnectionAsync(ct))
        {
            _log.LogError("[SP_LIST] Connexion SharePoint impossible.");
            return new ListSharePointPendingFilesResult(
                Success: false, Year: year, Month: month,
                PendingFiles: [], AlreadyLoadedFiles: [],
                ErrorMessage: "Connexion SharePoint impossible.");
        }

        // ?? 2. Lister les fichiers sur SharePoint ?????????????????????????
        var inboundPath = _helper.BuildInboundPath(year, month);
        var files = await _fileSource.ListFilesAsync(inboundPath, _settings.FilePattern, ct);

        if (query.FileNameFilter?.Count > 0)
            files = files.Where(f => query.FileNameFilter.Contains(f.FileName)).ToList();

        _log.LogInformation("[SP_LIST] {Count} fichier(s) trouvé(s) dans {Path}", files.Count, inboundPath);

        // ?? 3. Charger les noms déjà traités avec succès ??????????????????
        var alreadyLoaded = await _uow.IngestionFiles
            .GetAlreadyLoadedFileNamesAsync(year, month, ct);

        // ?? 4. Partitionner ???????????????????????????????????????????????
        var pending = new List<SharePointFileInfo>();
        var loaded = new List<SharePointFileInfo>();

        foreach (var f in files)
        {
            var info = new SharePointFileInfo(
                f.FileName, f.FullRemotePath, f.SizeBytes, f.LastModified);

            if (alreadyLoaded.Contains(f.FileName))
                loaded.Add(info);
            else
            {
                // Valider le nom — les fichiers avec erreurs bloquantes ne sont pas "pending"
                var nameCheck = FileNameValidator.Validate(
                    f.FileName, _settings.AllowedFilePrefixes, _settings.AllowedExtensions);
                if (nameCheck.IsValid)
                    pending.Add(info);
                else
                    _log.LogWarning("[SP_LIST] Nom invalide ignoré : {File}", f.FileName);
            }
        }

        _log.LogInformation(
            "[SP_LIST] Résultat — {Pending} pending, {Loaded} déjà traités",
            pending.Count, loaded.Count);

        return new ListSharePointPendingFilesResult(
            Success: true,
            Year: year,
            Month: month,
            PendingFiles: pending,
            AlreadyLoadedFiles: loaded);
    }
}
