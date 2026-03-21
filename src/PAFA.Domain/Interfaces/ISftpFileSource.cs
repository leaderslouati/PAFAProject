// ════════════════════════════════════════════════════════════
// PAFA.Domain/Interfaces/ISftpFileSource.cs
//
// Contrat défini dans le Domain — aucune dépendance SSH.NET.
// L'Application (PAFA.Extraction) dépend de cette interface.
// PAFA.Infrastructure l'implémente avec SSH.NET.
// ════════════════════════════════════════════════════════════
namespace PAFA.Domain.Interfaces;

/// <summary>
/// Contrat d'accès à une source de fichiers SFTP.
/// Implémenté dans PAFA.Infrastructure avec SSH.NET.
/// </summary>
public interface ISftpFileSource
{
    /// <summary>
    /// Liste les fichiers disponibles sur le serveur distant.
    /// </summary>
    Task<IReadOnlyList<SftpFileEntry>> ListFilesAsync(
        string remotePath,
        string filePattern = "*.xlsx",
        CancellationToken ct = default);

    /// <summary>
    /// Télécharge un fichier en mémoire.
    /// </summary>
    Task<byte[]> DownloadFileAsync(
        string remotePath,
        CancellationToken ct = default);

    /// <summary>
    /// Déplace un fichier (ex: de /upload vers /processed après import réussi).
    /// </summary>
    Task MoveFileAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken ct = default);

    /// <summary>Test de connectivité.</summary>
    Task<bool> TestConnectionAsync(CancellationToken ct = default);
}

/// <summary>
/// Représente un fichier disponible sur le serveur SFTP.
/// </summary>
public sealed record SftpFileEntry(
    string FileName,
    string FullRemotePath,
    long SizeBytes,
    DateTime LastModified);