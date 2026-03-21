using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using PAFA.Domain.Interfaces;

namespace PAFA.Infrastructure.Storage;

/// <summary>
/// IBlobStorageService implementation using MinIO (S3-compatible).
/// Used in POC/Docker to simulate Azure Blob Storage.
/// Files are stored as: {container}/{yyyy/MM}/{fileName}
/// </summary>
public sealed class MinioBlobStorageService : IBlobStorageService
{
    private readonly IMinioClient _client;
    private readonly ILogger<MinioBlobStorageService> _log;

    public MinioBlobStorageService(
        IOptions<BlobStorageSettings> opts,
        ILogger<MinioBlobStorageService> log)
    {
        _log = log;
        var s = opts.Value;

        _client = new MinioClient()
            .WithEndpoint(s.Endpoint)
            .WithCredentials(s.AccessKey, s.SecretKey)
            .WithSSL(s.UseSsl)
            .Build();
    }

    public async Task<string> UploadAsync(
        string fileName, byte[] content,
        string container = "landing-zone",
        CancellationToken ct = default)
    {
        // Ensure bucket exists
        var bucketExists = await _client.BucketExistsAsync(
            new BucketExistsArgs().WithBucket(container), ct);

        if (!bucketExists)
        {
            await _client.MakeBucketAsync(
                new MakeBucketArgs().WithBucket(container), ct);
            _log.LogInformation("Created MinIO bucket: {Bucket}", container);
        }

        // Object path with date partitioning
        var objectName = $"{DateTime.UtcNow:yyyy/MM}/{fileName}";

        using var stream = new MemoryStream(content);
        await _client.PutObjectAsync(new PutObjectArgs()
            .WithBucket(container)
            .WithObject(objectName)
            .WithStreamData(stream)
            .WithObjectSize(content.Length)
            .WithContentType("application/octet-stream"), ct);

        var path = $"{container}/{objectName}";
        _log.LogInformation("Uploaded to MinIO: {Path} ({Size:N0} bytes)",
            path, content.Length);

        return path;
    }

    public async Task<byte[]> DownloadAsync(string blobPath, CancellationToken ct = default)
    {
        // blobPath format: "landing-zone/2025/02/MOD520A_Feb25.xlsx"
        var slash = blobPath.IndexOf('/');
        var bucket = blobPath[..slash];
        var objectName = blobPath[(slash + 1)..];

        using var ms = new MemoryStream();
        await _client.GetObjectAsync(new GetObjectArgs()
            .WithBucket(bucket)
            .WithObject(objectName)
            .WithCallbackStream(stream => stream.CopyTo(ms)), ct);

        return ms.ToArray();
    }

    public async Task<bool> HealthCheckAsync(CancellationToken ct = default)
    {
        try
        {
            // List buckets as a connectivity check
            await _client.ListBucketsAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "MinIO health check failed");
            return false;
        }
    }
}
