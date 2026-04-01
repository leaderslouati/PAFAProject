using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PAFA.Domain.Interfaces;

namespace PAFA.Infrastructure.Storage;

/// <summary>
/// IBlobStorageService implementation using local filesystem.
/// Fallback for development without Docker/MinIO.
/// </summary>
public sealed class LocalBlobStorageService : IBlobStorageService
{
    private readonly string _basePath;
    private readonly ILogger<LocalBlobStorageService> _log;

    public LocalBlobStorageService(
        IOptions<BlobStorageSettings> opts,
        ILogger<LocalBlobStorageService> log)
    {
        _basePath = Path.GetFullPath(opts.Value.LocalPath);
        _log = log;
        Directory.CreateDirectory(_basePath);
    }

    public async Task<string> UploadAsync(
        string fileName,
        Stream content,
        string container = "landing-zone",
        int? year = null,
        int? month = null,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var folderYear  = year  ?? now.Year;
        var folderMonth = month ?? now.Month;
        var dir = Path.Combine(_basePath, container, $"{folderYear:D4}/{folderMonth:D2}");
        Directory.CreateDirectory(dir);

        var fullPath = Path.Combine(dir, fileName);

        // ── CORRECTION : Utilisation de FileStream et CopyToAsync ──
        using (var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
        {
            await content.CopyToAsync(fileStream, ct);
        }

        // Return relative path (same format as MinIO for consistency)
        var relativePath = $"{container}/{folderYear:D4}/{folderMonth:D2}/{fileName}";

        // Petite sécurité : certains flux (comme les flux réseaux) ne supportent pas la propriété .Length
        // On vérifie si on peut lire la taille, sinon on prend la taille du fichier créé sur le disque.
        long size = content.CanSeek ? content.Length : new FileInfo(fullPath).Length;

        _log.LogInformation("Saved to local storage: {Path} ({Size:N0} bytes)", relativePath, size);

        return relativePath;
    }
    public Task<Stream> DownloadStreamAsync(string blobPath, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_basePath, blobPath.Replace('/', Path.DirectorySeparatorChar));

        // File.OpenRead retourne un FileStream, ce qui est parfait !
        Stream stream = File.OpenRead(fullPath);
        return Task.FromResult(stream);
    }

    public Task<string> MoveAsync(
        string sourceBlobPath,
        string destinationBlobPath,
        CancellationToken ct = default)
    {
        var srcFull = Path.Combine(_basePath, sourceBlobPath.Replace('/', Path.DirectorySeparatorChar));
        var dstFull = Path.Combine(_basePath, destinationBlobPath.Replace('/', Path.DirectorySeparatorChar));

        Directory.CreateDirectory(Path.GetDirectoryName(dstFull)!);
        File.Move(srcFull, dstFull, overwrite: true);

        _log.LogInformation("Moved locally: {Src} → {Dst}", sourceBlobPath, destinationBlobPath);
        return Task.FromResult(destinationBlobPath);
    }
    public Task<bool> HealthCheckAsync(CancellationToken ct = default)
    {
        var ok = Directory.Exists(_basePath);
        return Task.FromResult(ok);
    }
}
