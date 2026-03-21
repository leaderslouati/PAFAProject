namespace PAFA.Infrastructure.Sftp;

public class SftpSettings
{
    public const string SectionName = "Sftp";

    /// <summary>Hôte SFTP. POC : "localhost". Production : adresse Xoserve.</summary>
    public string Host { get; set; } = "localhost";

    /// <summary>Port. POC : 2222 (Docker). Production : 22.</summary>
    public int Port { get; set; } = 2222;

    public string Username { get; set; } = "xoserve";
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Dossier distant des nouveaux fichiers.
    /// POC Docker : /upload (relatif à /home/xoserve).
    /// </summary>
    public string RemotePath { get; set; } = "/upload";

    /// <summary>Dossier distant après traitement réussi.</summary>
    public string ProcessedPath { get; set; } = "/processed";

    /// <summary>Dossier pour les fichiers en erreur (ne seront pas retraités).</summary>
    public string FailedPath { get; set; } = "/failed";

    /// <summary>Pattern de fichiers à traiter.</summary>
    public string FilePattern { get; set; } = "*.xlsx";
}
