namespace PAFA.Domain.Interfaces;

/// <summary>
/// Contrat pour le stockage de fichiers bruts (Azure Blob en prod, MinIO/Local en POC).
/// D�fini dans Domain � aucune d�pendance infra.
/// </summary>
public interface IBlobStorageService
{
    /// <summary>
    /// Upload un fichier brut. Retourne le chemin/URI du blob stock�.
    /// </summary>
    Task<string> UploadAsync(
        string fileName,
        Stream content,
        string container = "landing-zone",
        int? year = null,
        int? month = null,
        CancellationToken ct = default);

    /// <summary>
    /// T�l�charge un fichier depuis le stockage sous forme de flux (Stream)
    /// pour �viter la surcharge m�moire des gros fichiers.
    /// </summary>
    Task<Stream> DownloadStreamAsync(string blobPath, CancellationToken ct = default);

    /// <summary>
    /// D�place un objet d'un chemin source vers un chemin destination (Copy + Delete).
    /// Retourne le nouveau chemin.
    /// </summary>
    Task<string> MoveAsync(string sourceBlobPath, string destinationBlobPath, CancellationToken ct = default);

    /// <summary>
    /// V�rifie que le service de stockage est accessible.
    /// </summary>
    Task<bool> HealthCheckAsync(CancellationToken ct = default);
    /// <summary>
    /// Génère une URL à accès en lecture seule pour le chemin blob spécifié (fichier ou dossier).
    /// En production : SAS token Azure Blob. En développement local : chemin système de fichiers.
    /// </summary>
    Task<string> GenerateReadUrlAsync(string blobPath, TimeSpan? expiry = null, CancellationToken ct = default);}