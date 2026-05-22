using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using PAFA.Domain.Models;

namespace PAFA.Infrastructure.SharePoint;

/// <summary>
/// Implémentation de IRemoteFileSource pour SharePoint Online via Microsoft Graph API v5.
/// Utilise Client Credentials (App-Only) pour l'authentification OAuth 2.0.
/// </summary>
public sealed class SharePointFileSource : IRemoteFileSource
{
    private readonly SharePointSettings _cfg;
    private readonly ILogger<SharePointFileSource> _log;

    // Instance partagée pour éviter le socket exhaustion et permettre de retourner des flux réseau ouverts.
    private static readonly HttpClient _httpClient = new HttpClient();

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

    // ───────────────────────────────────────────────────────────────
    //  Résolution du Drive ID au démarrage
    // ───────────────────────────────────────────────────────────────

    private async Task<string> ResolveDriveIdAsync(GraphServiceClient graph, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(_cfg.DriveId))
            return _cfg.DriveId;

        var drive = await graph.Sites[_cfg.SiteId]
            .Drive
            .GetAsync(cancellationToken: ct);

        return drive!.Id!;
    }

    // ───────────────────────────────────────────────────────────────
    //  1. Lister les fichiers
    // ───────────────────────────────────────────────────────────────

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

    // ───────────────────────────────────────────────────────────────
    //  2. Télécharger un fichier (CORRIGÉ POUR LE STREAMING)
    // ───────────────────────────────────────────────────────────────

    public async Task<Stream> DownloadFileAsync(
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
            // Retourne directement le flux réseau. 
            // On utilise _httpClient statique pour que la connexion survive à la méthode.
            var stream = await _httpClient.GetStreamAsync(downloadUrl, ct);
            _log.LogDebug("Téléchargement (large file) initié via Stream : {Path}", remotePath);
            return stream;
        }

        // Fichier ≤ 4 Mo — download direct via Graph API qui retourne nativement un Stream
        var graphStream = await graph.Drives[driveId]
            .Root
            .ItemWithPath(remotePath)
            .Content
            .GetAsync(cancellationToken: ct);

        _log.LogDebug("Téléchargement initié via Stream GraphAPI : {Path}", remotePath);

        // On retourne directement le flux, c'est l'appelant (le Handler) qui le fermera avec le "using" !
        return graphStream!;
    }

    // ───────────────────────────────────────────────────────────────
    //  3. Déplacer un fichier
    // ───────────────────────────────────────────────────────────────

    public async Task MoveFileAsync(
        string sourcePath, string destinationPath, CancellationToken ct = default)
    {
        var graph = BuildClient();
        var driveId = await ResolveDriveIdAsync(graph, ct);

        var destFolder = Path.GetDirectoryName(destinationPath)!.Replace('\\', '/');
        var destName = Path.GetFileName(destinationPath);

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

        await graph.Drives[driveId]
            .Root
            .ItemWithPath(sourcePath)
            .PatchAsync(new DriveItem
            {
                ParentReference = new ItemReference { Id = destItem!.Id },
                Name = destName
            }, cancellationToken: ct);

        _log.LogDebug("Déplacé : {Src} → {Dst}", sourcePath, destinationPath);
    }

    // ───────────────────────────────────────────────────────────────
    //  4. Test de connectivité
    // ───────────────────────────────────────────────────────────────

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        var result = await TestConnectionDetailedAsync(ct);
        return result.IsConnected;
    }

    public async Task<ConnectionTestResult> TestConnectionDetailedAsync(CancellationToken ct = default)
    {
        try
        {
            var graph = BuildClient();
            var site = await graph.Sites[_cfg.SiteId]
                .GetAsync(cancellationToken: ct);

            _log.LogInformation("SharePoint connexion OK — Site: {Name} ({Url})",
                site?.DisplayName, site?.WebUrl);
            return site != null
                ? ConnectionTestResult.Success()
                : ConnectionTestResult.Unknown("Site returned null.");
        }
        catch (AuthenticationFailedException ex)
        {
            _log.LogError(ex, "SharePoint authentication failed — credentials invalid or expired. TenantId: {TenantId}", _cfg.TenantId);
            return ConnectionTestResult.AuthenticationFailure(
                $"Authentication failed: {ex.Message}. Check ClientId/ClientSecret/TenantId.");
        }
        catch (ODataError ex) when (ex.ResponseStatusCode == 401)
        {
            _log.LogError(ex, "SharePoint 401 Unauthorized — TenantId: {TenantId}, SiteId: {SiteId}", _cfg.TenantId, _cfg.SiteId);
            return ConnectionTestResult.AuthenticationFailure(
                $"HTTP 401 Unauthorized: {ex.Message}");
        }
        catch (ODataError ex) when (ex.ResponseStatusCode == 403)
        {
            _log.LogError(ex, "SharePoint 403 Forbidden — insufficient permissions. SiteId: {SiteId}", _cfg.SiteId);
            return ConnectionTestResult.Forbidden(
                $"HTTP 403 Forbidden: insufficient permissions for site '{_cfg.SiteId}'.");
        }
        catch (HttpRequestException ex)
        {
            _log.LogError(ex, "SharePoint network error — TenantId: {TenantId}, SiteId: {SiteId}", _cfg.TenantId, _cfg.SiteId);
            return ConnectionTestResult.NetworkError($"Network error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "SharePoint connexion échouée — TenantId: {TenantId}, SiteId: {SiteId}",
                _cfg.TenantId, _cfg.SiteId);
            return ConnectionTestResult.Unknown(ex.Message);
        }
    }

    // ───────────────────────────────────────────────────────────────
    //  5. Patch SharePoint processing status
    // ───────────────────────────────────────────────────────────────

    public async Task PatchStatusAsync(string remotePath, string status, CancellationToken ct = default)
    {
        try
        {
            var graph   = BuildClient();
            var driveId = await ResolveDriveIdAsync(graph, ct);

            // Fetch the drive item and request SharePoint list identifiers
            var item = await graph.Drives[driveId]
                .Root
                .ItemWithPath(remotePath)
                .GetAsync(q => q.QueryParameters.Select = ["id", "sharepointIds"], ct);

            var listId     = item?.SharepointIds?.ListId;
            var listItemId = item?.SharepointIds?.ListItemId;

            if (listId is null || listItemId is null)
            {
                _log.LogWarning(
                    "Cannot patch SharePoint status for {Path} — SharePoint list IDs not resolved",
                    remotePath);
                return;
            }

            await graph.Sites[_cfg.SiteId]
                .Lists[listId]
                .Items[listItemId]
                .Fields
                .PatchAsync(
                    new FieldValueSet
                    {
                        AdditionalData = new Dictionary<string, object>
                        {
                            ["ProcessingStatus"] = status
                        }
                    },
                    cancellationToken: ct);

            _log.LogInformation(
                "SharePoint status patched: {Path} \u2192 {Status}", remotePath, status);
        }
        catch (ODataError ex) when (ex.ResponseStatusCode == 404)
        {
            _log.LogWarning(
                "SharePoint PATCH skipped \u2014 item not found at {Path}: {Error}",
                remotePath, ex.Message);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "SharePoint status patch failed for {Path}", remotePath);
            throw;
        }
    }

    // ───────────────────────────────────────────────────────────────
    //  Helpers
    // ───────────────────────────────────────────────────────────────

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
            return ext is ".xlsx" or ".xls";
        }

        if (pattern.StartsWith("*"))
        {
            var ext = pattern.TrimStart('*');
            return fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase);
        }

        return fileName.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }
}