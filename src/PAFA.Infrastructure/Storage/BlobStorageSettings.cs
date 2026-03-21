namespace PAFA.Infrastructure.Storage;

public class BlobStorageSettings
{
    public const string SectionName = "BlobStorage";

    /// <summary>"MinIO" | "Local" | "Azure" (future)</summary>
    public string Provider { get; set; } = "Local";

    /// <summary>Local filesystem path (for Provider=Local)</summary>
    public string LocalPath { get; set; } = "./storage";

    /// <summary>MinIO/S3 endpoint (for Provider=MinIO)</summary>
    public string Endpoint { get; set; } = "localhost:9000";

    /// <summary>MinIO access key</summary>
    public string AccessKey { get; set; } = "minioadmin";

    /// <summary>MinIO secret key</summary>
    public string SecretKey { get; set; } = "minioadmin";

    /// <summary>Use SSL for MinIO connection</summary>
    public bool UseSsl { get; set; } = false;
}
