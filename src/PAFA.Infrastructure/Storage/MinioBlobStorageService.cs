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
    private readonly string _basePath;
    private readonly string _baseBucket;
    public MinioBlobStorageService(
        IOptions<BlobStorageSettings> opts,
        ILogger<MinioBlobStorageService> log)
    {
        _log = log;
        var s = opts.Value;
        _basePath = Path.GetFullPath(opts.Value.LocalPath);
        _baseBucket = string.IsNullOrWhiteSpace(s.BaseBucketName) ? "data" : s.BaseBucketName;
        _client = new MinioClient()
            .WithEndpoint(s.Endpoint)
            .WithCredentials(s.AccessKey, s.SecretKey)
            .WithSSL(s.UseSsl)
            .Build();
    }

    public async Task<string> UploadAsync(
        string fileName, Stream content,
        string container = "landing-zone",
        int? year = null,
        int? month = null,
        CancellationToken ct = default)
    {


        // Map container -> bucket/prefix. If BaseBucketName is set we store under
        // that bucket using a semantic prefix (inbound/processing/archive/quarantine)
        var now = DateTime.UtcNow;
        var folderYear  = year  ?? now.Year;
        var folderMonth = month ?? now.Month;

        // Map legacy container names to prefixes (match desired layout under base bucket)
        var prefix = container switch
        {
            "landing-zone" => "inbound",
            "processed"    => "processed",
            "failed"       => "failed",
            "processing"   => "processed",
            _ => container
        };

        var objectName = $"{prefix}/{folderYear:D4}/{folderMonth:D2}/{fileName}";

        // Use base bucket as the single bucket root
        var bucket = _baseBucket;

        // C#
// Avant PutObjectAsync, vérifier/créer le bucket réel utilisé (_baseBucket)
        if (!await _client.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket), ct))
        {
            await _client.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket), ct);
            _log.LogInformation("Created MinIO bucket: {Bucket}", bucket);
        }
        await _client.PutObjectAsync(new PutObjectArgs()
            .WithBucket(bucket)
            .WithObject(objectName)
            .WithStreamData(content)
            .WithObjectSize(content.Length)
            .WithContentType("application/octet-stream"), ct);

        var path = $"{bucket}/{objectName}";
        _log.LogInformation("Uploaded to MinIO: {Path} ({Size:N0} bytes)",
            path, content.Length);

        return path;
    }
    public async Task<Stream> DownloadStreamAsync(string blobPath, CancellationToken ct = default)
    {
        // blobPath format: "{bucket}/{prefix/.../fileName}" or legacy
        // "{container}/{yyyy/MM/fileName}". Accept both.
        var slashIndex = blobPath.IndexOf('/');
        if (slashIndex < 0)
            throw new ArgumentException($"Invalid blobPath format (expected 'bucket/...'): {blobPath}");

        var bucket     = blobPath[..slashIndex];
        var objectName = blobPath[(slashIndex + 1)..];
        var memStream = new MemoryStream();
        await _client.GetObjectAsync(new GetObjectArgs()
            .WithBucket(bucket)
            .WithObject(objectName)
            .WithCallbackStream(s => s.CopyTo(memStream)), ct);

        memStream.Position = 0;
        _log.LogInformation("Downloaded from MinIO: {BlobPath} ({Size:N0} bytes)", blobPath, memStream.Length);
        return memStream;
    }

    public async Task<string> MoveAsync(
        string sourceBlobPath,
        string destinationBlobPath,
        CancellationToken ct = default)
    {
        // Normalize source/destination to base bucket + object path. Support
        // legacy values like "landing-zone/..." by mapping to prefixes inside
        // the base bucket.
        static string Normalize(string path, string baseBucket)
        {
            var idx = path.IndexOf('/');
            if (idx < 0) throw new ArgumentException($"Invalid blobPath: {path}");
            var first = path[..idx];
            var rest  = path[(idx + 1)..];

            return first switch
            {
                "landing-zone" => $"{baseBucket}/inbound/{rest}",
                "processed"    => $"{baseBucket}/processed/{rest}",
                "failed"       => $"{baseBucket}/failed/{rest}",
                "processing"   => $"{baseBucket}/processing/{rest}",
                _ => path.StartsWith(baseBucket + "/") ? path : $"{baseBucket}/{path}"
            };
        }

        var srcNorm = Normalize(sourceBlobPath, _baseBucket);
        var dstNorm = Normalize(destinationBlobPath, _baseBucket);

        string srcBucket = srcNorm[..srcNorm.IndexOf('/')];
        string srcObject = srcNorm[(srcNorm.IndexOf('/') + 1)..];
        string dstBucket = dstNorm[..dstNorm.IndexOf('/')];
        string dstObject = dstNorm[(dstNorm.IndexOf('/') + 1)..];

        // Ensure destination bucket exists
        if (!await _client.BucketExistsAsync(new BucketExistsArgs().WithBucket(dstBucket), ct))
        {
            await _client.MakeBucketAsync(new MakeBucketArgs().WithBucket(dstBucket), ct);
            _log.LogInformation("Created MinIO bucket: {Bucket}", dstBucket);
        }

        // Copy source ? destination
        await _client.CopyObjectAsync(new CopyObjectArgs()
            .WithBucket(dstBucket)
            .WithObject(dstObject)
            .WithCopyObjectSource(new CopySourceObjectArgs()
                .WithBucket(srcBucket)
                .WithObject(srcObject)), ct);

        // Delete source
        await _client.RemoveObjectAsync(new RemoveObjectArgs()
            .WithBucket(srcBucket)
            .WithObject(srcObject), ct);

        // Ensure inbound repository (year/month) remains present even if empty.
        // Create a small placeholder object under the same year/month prefix when
        // moving from inbound so the logical folder remains visible.
        try
        {
            var srcParts = srcObject.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (srcParts.Length >= 3 && string.Equals(srcParts[0], "inbound", StringComparison.OrdinalIgnoreCase))
            {
                var dir = string.Join('/', srcParts.Take(3)); // inbound/{year}/{month}
                var placeholder = $"{dir}/.keep";
                // Put a zero-byte object as a placeholder (idempotent - overwrites if exists)
                using var empty = new MemoryStream(Array.Empty<byte>());
                await _client.PutObjectAsync(new PutObjectArgs()
                    .WithBucket(srcBucket)
                    .WithObject(placeholder)
                    .WithStreamData(empty)
                    .WithObjectSize(0)
                    .WithContentType("application/x-placeholder"), ct);
            }
        }
        catch (Exception ex)
        {
            // Non-blocking: placeholder creation failure should not break the move.
            _log.LogWarning(ex, "Failed to create placeholder after moving object {Src}/{Obj}", srcBucket, srcObject);
        }

        var resultPath = $"{dstBucket}/{dstObject}";
        _log.LogInformation("Moved in MinIO: {Src} ? {Dst}", sourceBlobPath, resultPath);
        return resultPath;
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
