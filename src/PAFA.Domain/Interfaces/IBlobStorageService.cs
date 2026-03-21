namespace PAFA.Domain.Interfaces;

/// <summary>
/// Contrat pour le stockage de fichiers bruts (Azure Blob en prod, MinIO/Local en POC).
/// Défini dans Domain — aucune dépendance infra.
/// </summary>
public interface IBlobStorageService
{
    /// <summary>
    /// Upload un fichier brut. Retourne le chemin/URI du blob stocké.
    /// </summary>
    Task<string> UploadAsync(
        string fileName,
        byte[] content,
        string container = "landing-zone",
        CancellationToken ct = default);

    /// <summary>
    /// Télécharge un fichier depuis le stockage.
    /// </summary>
    Task<byte[]> DownloadAsync(string blobPath, CancellationToken ct = default);

    /// <summary>
    /// Vérifie que le service de stockage est accessible.
    /// </summary>
    Task<bool> HealthCheckAsync(CancellationToken ct = default);
}
