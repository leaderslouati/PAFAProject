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
        Stream content,
        string container = "landing-zone",
        int? year = null,
        int? month = null,
        CancellationToken ct = default);

    /// <summary>
    /// Télécharge un fichier depuis le stockage sous forme de flux (Stream)
    /// pour éviter la surcharge mémoire des gros fichiers.
    /// </summary>
    Task<Stream> DownloadStreamAsync(string blobPath, CancellationToken ct = default);

    /// <summary>
    /// Déplace un objet d'un chemin source vers un chemin destination (Copy + Delete).
    /// Retourne le nouveau chemin.
    /// </summary>
    Task<string> MoveAsync(string sourceBlobPath, string destinationBlobPath, CancellationToken ct = default);

    /// <summary>
    /// Vérifie que le service de stockage est accessible.
    /// </summary>
    Task<bool> HealthCheckAsync(CancellationToken ct = default);
}