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
        string fileName, byte[] content,
        string container = "landing-zone",
        CancellationToken ct = default)
    {
        var dir = Path.Combine(_basePath, container, DateTime.UtcNow.ToString("yyyy/MM"));
        Directory.CreateDirectory(dir);

        var fullPath = Path.Combine(dir, fileName);
        await File.WriteAllBytesAsync(fullPath, content, ct);

        // Return relative path (same format as MinIO for consistency)
        var relativePath = $"{container}/{DateTime.UtcNow:yyyy/MM}/{fileName}";
        _log.LogInformation("Saved to local storage: {Path} ({Size:N0} bytes)",
            relativePath, content.Length);

        return relativePath;
    }

    public async Task<byte[]> DownloadAsync(string blobPath, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_basePath, blobPath.Replace('/', Path.DirectorySeparatorChar));
        return await File.ReadAllBytesAsync(fullPath, ct);
    }

    public Task<bool> HealthCheckAsync(CancellationToken ct = default)
    {
        var ok = Directory.Exists(_basePath);
        return Task.FromResult(ok);
    }
}
