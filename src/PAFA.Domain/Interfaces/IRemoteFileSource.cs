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
    Task<byte[]> DownloadFileAsync(
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
}

/// <summary>
/// Représente un fichier disponible sur la source distante.
/// </summary>
public sealed record RemoteFileEntry(
    string FileName,
    string FullRemotePath,
    long SizeBytes,
    DateTime LastModified);

/// <summary>
/// Contrat de configuration pour une source de fichiers distante.
/// Défini dans Domain — implémenté par SharePointSettings, SftpSettings, etc.
/// 
/// Structure SharePoint cible :
///   {BaseInboundPath}/{Année}/{Mois}/   → ex: /PARR/2025/07/
///   {ProcessedPath}/{Année}/{Mois}/     → ex: /Processed/2025/07/
///   {FailedPath}/                       → ex: /Failed/
/// </summary>
public interface IFileSourceSettings
{
    /// <summary>
    /// Chemin de base des fichiers entrants.
    /// Ex: "" (racine) ou "/PARR".
    /// Le handler construit le chemin complet : {BaseInboundPath}/{year}/{month:D2}
    /// </summary>
    string BaseInboundPath { get; }

    /// <summary>Dossier racine de destination après traitement réussi.</summary>
    string ProcessedPath  { get; }

    /// <summary>Dossier pour les fichiers en erreur.</summary>
    string FailedPath     { get; }

    /// <summary>Pattern de fichiers à traiter.</summary>
    string FilePattern    { get; }
}