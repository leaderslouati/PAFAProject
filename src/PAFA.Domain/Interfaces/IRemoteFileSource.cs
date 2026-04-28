namespace PAFA.Domain.Interfaces;

/// <summary>
/// Contrat d'accès à une source de fichiers distante.
/// Implémenté dans PAFA.Infrastructure (SharePoint Online via Microsoft Graph).
/// </summary>
public interface IRemoteFileSource
{
    /// <summary>
    /// Liste les fichiers disponibles sur la source distante.
    /// </summary>
    Task<IReadOnlyList<RemoteFileEntry>> ListFilesAsync(
        string remotePath,
        string filePattern = "*.xlsx",
        CancellationToken ct = default);

    /// <summary>
    /// Télécharge un fichier en mémoire.
    /// </summary>
    Task<Stream> DownloadFileAsync(
        string remotePath,
        CancellationToken ct = default);

    /// <summary>
    /// Déplace un fichier (ex: de /2025/07 vers /processed/2025/07 après import réussi).
    /// </summary>
    Task MoveFileAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken ct = default);

    /// <summary>Test de connectivité.</summary>
    Task<bool> TestConnectionAsync(CancellationToken ct = default);

    /// <summary>
    /// Test de connectivité with detailed error classification (AC8).
    /// Distinguishes authentication failures from network/other errors.
    /// </summary>
    Task<PAFA.Domain.Models.ConnectionTestResult> TestConnectionDetailedAsync(CancellationToken ct = default);
}

/// <summary>
/// Représente un fichier disponible sur la source distante.
/// </summary>
public sealed record RemoteFileEntry(
    string FileName,
    string FullRemotePath,
    long SizeBytes,
    DateTime LastModified);

