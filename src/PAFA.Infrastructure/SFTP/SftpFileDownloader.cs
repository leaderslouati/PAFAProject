using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PAFA.Domain.Interfaces;
using Renci.SshNet;

namespace PAFA.Infrastructure.Sftp;

public sealed class SftpFileDownloader : ISftpFileSource
{
    private readonly SftpSettings _settings;
    private readonly ILogger<SftpFileDownloader> _log;

    public SftpFileDownloader(
        IOptions<SftpSettings> settings,
        ILogger<SftpFileDownloader> log)
    {
        _settings = settings.Value;
        _log = log;
    }

    public async Task<IReadOnlyList<SftpFileEntry>> ListFilesAsync(
        string remotePath, string filePattern = "*.xlsx", CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            using var client = CreateClient();
            client.Connect();

            try
            {
                // Crée le dossier distant s'il n'existe pas encore (ex: premier démarrage)
                if (!client.Exists(remotePath))
                {
                    _log.LogWarning("Dossier distant introuvable, création : {Path}", remotePath);
                    client.CreateDirectory(remotePath);
                }

                var files = client.ListDirectory(remotePath)
                    .Where(f => !f.IsDirectory && MatchesPattern(f.Name, filePattern))
                    .Select(f => new SftpFileEntry(
                        FileName: f.Name,
                        FullRemotePath: f.FullName,
                        SizeBytes: f.Length,
                        LastModified: f.LastWriteTime))
                    .ToList();

                _log.LogDebug("{Count} fichiers listés dans {Path}", files.Count, remotePath);
                return (IReadOnlyList<SftpFileEntry>)files;
            }
            finally
            {
                client.Disconnect();
            }
        }, ct);
    }

    public async Task<byte[]> DownloadFileAsync(
        string remotePath, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            using var client = CreateClient();
            client.Connect();

            try
            {
                using var ms = new MemoryStream();
                client.DownloadFile(remotePath, ms);
                var bytes = ms.ToArray();

                _log.LogDebug("Téléchargé : {Path} ({Size:N0} bytes)", remotePath, bytes.Length);
                return bytes;
            }
            finally
            {
                client.Disconnect();
            }
        }, ct);
    }

    public async Task MoveFileAsync(
        string sourcePath, string destinationPath, CancellationToken ct = default)
    {
        await Task.Run(() =>
        {
            using var client = CreateClient();
            client.Connect();

            try
            {
                // Créer le dossier destination si nécessaire
                var dir = Path.GetDirectoryName(destinationPath)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(dir) && !client.Exists(dir))
                    client.CreateDirectory(dir);

                using (var inStream = client.OpenRead(sourcePath))
                using (var outStream = client.OpenWrite(destinationPath))
                {
                    inStream.CopyTo(outStream);
                    outStream.Flush();
                }
                client.DeleteFile(sourcePath);

                _log.LogDebug("Déplacé : {Src} → {Dst}", sourcePath, destinationPath);
            }
            finally
            {
                client.Disconnect();
            }
        }, ct);
    }

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var client = CreateClient();
                client.Connect();
                var ok = client.IsConnected;
                client.Disconnect();
                _log.LogInformation("SFTP connexion OK — {Host}:{Port}", _settings.Host, _settings.Port);
                return ok;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "SFTP connexion échouée — {Host}:{Port}", _settings.Host, _settings.Port);
                return false;
            }
        }, ct);
    }

    // ── Helper ───────────────────────────────────────────────
    private SftpClient CreateClient()
        => new(_settings.Host, _settings.Port, _settings.Username, _settings.Password);

    private static bool MatchesPattern(string fileName, string pattern)
    {
        if (pattern.StartsWith("*"))
        {
            var ext = pattern.TrimStart('*');
            return fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase);
        }
        return fileName.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }
}