using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using PAFA.Domain.Interfaces;

namespace PAFA.Infrastructure.SharePoint;

/// <summary>
/// Implémentation de IRemoteFileSource pour SharePoint Online via Microsoft Graph API v5.
/// Utilise Client Credentials (App-Only) pour l'authentification OAuth 2.0.
/// </summary>
public sealed class SharePointFileSource : IRemoteFileSource
{
    private readonly SharePointSettings _cfg;
    private readonly ILogger<SharePointFileSource> _log;

    public SharePointFileSource(
        IOptions<SharePointSettings> cfg,
        ILogger<SharePointFileSource> log)
    {
        _cfg = cfg.Value;
        _log = log;
    }

    private GraphServiceClient BuildClient()
    {
        var credential = new ClientSecretCredential(
            _cfg.TenantId, _cfg.ClientId, _cfg.ClientSecret);
        return new GraphServiceClient(credential);
    }

    // ???????????????????????????????????????????????????????????????
    //  Résolution du Drive ID au démarrage
    // ???????????????????????????????????????????????????????????????

    /// <summary>
    /// Retourne le Drive ID configuré, ou le drive par défaut du site.
    /// </summary>
    private async Task<string> ResolveDriveIdAsync(GraphServiceClient graph, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(_cfg.DriveId))
            return _cfg.DriveId;

        var drive = await graph.Sites[_cfg.SiteId]
            .Drive
            .GetAsync(cancellationToken: ct);

        return drive!.Id!;
    }

    // ???????????????????????????????????????????????????????????????
    //  1. Lister les fichiers
    // ???????????????????????????????????????????????????????????????

    public async Task<IReadOnlyList<RemoteFileEntry>> ListFilesAsync(
        string remotePath, string filePattern = "*.xlsx", CancellationToken ct = default)
    {
        var graph = BuildClient();
        var driveId = await ResolveDriveIdAsync(graph, ct);

        var children = await graph.Drives[driveId]
            .Root
            .ItemWithPath(remotePath)
            .Children
            .GetAsync(cancellationToken: ct);

        var allItems = new List<DriveItem>();
        if (children?.Value != null)
            allItems.AddRange(children.Value);

        // Gestion pagination — Graph retourne max 200 items par page
        while (children?.OdataNextLink != null)
        {
            children = await graph.Drives[driveId]
                .Root
                .ItemWithPath(remotePath)
                .Children
                .WithUrl(children.OdataNextLink)
                .GetAsync(cancellationToken: ct);

            if (children?.Value != null)
                allItems.AddRange(children.Value);
        }

        var result = allItems
            .Where(i => i.File != null && MatchesPattern(i.Name!, filePattern))
            .Select(i => new RemoteFileEntry(
                FileName: i.Name!,
                FullRemotePath: $"{remotePath.TrimEnd('/')}/{i.Name}",
                SizeBytes: i.Size ?? 0,
                LastModified: i.LastModifiedDateTime?.DateTime ?? DateTime.UtcNow))
            .ToList();

        _log.LogDebug("{Count} fichier(s) listé(s) dans SharePoint {Path}", result.Count, remotePath);
        return result;
    }

    // ???????????????????????????????????????????????????????????????
    //  2. Télécharger un fichier
    // ???????????????????????????????????????????????????????????????

    public async Task<byte[]> DownloadFileAsync(
        string remotePath, CancellationToken ct = default)
    {
        var graph = BuildClient();
        var driveId = await ResolveDriveIdAsync(graph, ct);

        // Récupérer les métadonnées pour vérifier la taille
        var item = await graph.Drives[driveId]
            .Root
            .ItemWithPath(remotePath)
            .GetAsync(cancellationToken: ct);

        // Pour les gros fichiers (> 4 Mo), utiliser @microsoft.graph.downloadUrl
        if (item?.Size > 4 * 1024 * 1024 &&
            item.AdditionalData != null &&
            item.AdditionalData.TryGetValue("@microsoft.graph.downloadUrl", out object? urlObj) &&
            urlObj is string downloadUrl &&
            !string.IsNullOrEmpty(downloadUrl))
        {
            using var http = new HttpClient();
            var bytes = await http.GetByteArrayAsync(downloadUrl, ct);
            _log.LogDebug("Téléchargé (large file) : {Path} ({Size:N0} bytes)", remotePath, bytes.Length);
            return bytes;
        }

        // Fichier ? 4 Mo — download direct via Graph
        var stream = await graph.Drives[driveId]
            .Root
            .ItemWithPath(remotePath)
            .Content
            .GetAsync(cancellationToken: ct);

        using var ms = new MemoryStream();
        await stream!.CopyToAsync(ms, ct);
        var result = ms.ToArray();

        _log.LogDebug("Téléchargé : {Path} ({Size:N0} bytes)", remotePath, result.Length);
        return result;
    }

    // ???????????????????????????????????????????????????????????????
    //  3. Déplacer un fichier
    // ???????????????????????????????????????????????????????????????

    public async Task MoveFileAsync(
        string sourcePath, string destinationPath, CancellationToken ct = default)
    {
        var graph = BuildClient();
        var driveId = await ResolveDriveIdAsync(graph, ct);

        var destFolder = Path.GetDirectoryName(destinationPath)!.Replace('\\', '/');
        var destName = Path.GetFileName(destinationPath);

        // Récupérer le dossier destination (ou le créer)
        DriveItem? destItem;
        try
        {
            destItem = await graph.Drives[driveId]
                .Root
                .ItemWithPath(destFolder)
                .GetAsync(cancellationToken: ct);
        }
        catch (ODataError ex) when (ex.ResponseStatusCode == 404)
        {
            _log.LogWarning("Dossier destination introuvable, création : {Path}", destFolder);
            destItem = await CreateFolderRecursiveAsync(graph, driveId, destFolder, ct);
        }

        // PATCH pour déplacer le fichier
        await graph.Drives[driveId]
            .Root
            .ItemWithPath(sourcePath)
            .PatchAsync(new DriveItem
            {
                ParentReference = new ItemReference { Id = destItem!.Id },
                Name = destName
            }, cancellationToken: ct);

        _log.LogDebug("Déplacé : {Src} ? {Dst}", sourcePath, destinationPath);
    }

    // ???????????????????????????????????????????????????????????????
    //  4. Test de connectivité
    // ???????????????????????????????????????????????????????????????

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            var graph = BuildClient();
            var site = await graph.Sites[_cfg.SiteId]
                .GetAsync(cancellationToken: ct);

            _log.LogInformation("SharePoint connexion OK — Site: {Name} ({Url})",
                site?.DisplayName, site?.WebUrl);
            return site != null;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "SharePoint connexion échouée — TenantId: {TenantId}, SiteId: {SiteId}",
                _cfg.TenantId, _cfg.SiteId);
            return false;
        }
    }

    // ???????????????????????????????????????????????????????????????
    //  Helpers
    // ???????????????????????????????????????????????????????????????

    /// <summary>
    /// Crée un dossier récursivement dans SharePoint.
    /// Ex: "processed/2025/07" ? crée processed, puis 2025, puis 07.
    /// </summary>
    private async Task<DriveItem> CreateFolderRecursiveAsync(
        GraphServiceClient graph, string driveId, string folderPath, CancellationToken ct)
    {
        var segments = folderPath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var currentPath = "";
        DriveItem? current = null;

        foreach (var segment in segments)
        {
            var parentPath = currentPath;
            currentPath = string.IsNullOrEmpty(currentPath) ? segment : $"{currentPath}/{segment}";

            try
            {
                current = await graph.Drives[driveId]
                    .Root
                    .ItemWithPath(currentPath)
                    .GetAsync(cancellationToken: ct);
            }
            catch (ODataError ex) when (ex.ResponseStatusCode == 404)
            {
                var newFolder = new DriveItem
                {
                    Name = segment,
                    Folder = new Folder(),
                    AdditionalData = new Dictionary<string, object>
                    {
                        ["@microsoft.graph.conflictBehavior"] = "fail"
                    }
                };

                if (string.IsNullOrEmpty(parentPath))
                {
                    // Créer à la racine du drive
                    current = await graph.Drives[driveId]
                        .Items["root"]
                        .Children
                        .PostAsync(newFolder, cancellationToken: ct);
                }
                else
                {
                    current = await graph.Drives[driveId]
                        .Root
                        .ItemWithPath(parentPath)
                        .Children
                        .PostAsync(newFolder, cancellationToken: ct);
                }

                _log.LogInformation("Dossier créé dans SharePoint : {Path}", currentPath);
            }
        }

        return current!;
    }

    private static bool MatchesPattern(string fileName, string pattern)
    {
        if (fileName.StartsWith('.')) return false;

        if (pattern == "*")
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            return ext is ".xlsx" or ".xls" or ".csv" or ".xml";
        }

        if (pattern.StartsWith("*"))
        {
            var ext = pattern.TrimStart('*');
            return fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase);
        }

        return fileName.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }
}
