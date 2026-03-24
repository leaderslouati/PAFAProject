using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PAFA.Domain.Interfaces;
using PAFA.Infrastructure.Persistence;

namespace PAFA.Api.Controllers;

/// <summary>
/// Health check endpoint — vérifie la connectivité de tous les composants.
/// Utilisé par Kubernetes liveness/readiness probes et monitoring.
///
/// GET /api/health       ? check rapide (DB only)
/// GET /api/health/full  ? check complet (DB + SharePoint + MinIO)
/// </summary>
[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    private readonly PafaDbContext _db;
    private readonly IRemoteFileSource _fileSource;
    private readonly IBlobStorageService _blob;
    private readonly ILogger<HealthController> _log;

    public HealthController(
        PafaDbContext db,
        IRemoteFileSource fileSource,
        IBlobStorageService blob,
        ILogger<HealthController> log)
    {
        _db = db;
        _fileSource = fileSource;
        _blob = blob;
        _log = log;
    }

    /// <summary>
    /// GET /api/health — check rapide (DB uniquement).
    /// Utilisé par Kubernetes liveness probe.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetHealth(CancellationToken ct)
    {
        var dbOk = false;
        try
        {
            dbOk = await _db.Database.CanConnectAsync(ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Health check: DB connection failed");
        }

        var result = new
        {
            status = dbOk ? "healthy" : "unhealthy",
            timestamp = DateTime.UtcNow,
            checks = new { database = dbOk }
        };

        return dbOk ? Ok(result) : StatusCode(503, result);
    }

    /// <summary>
    /// GET /api/health/full — check complet (DB + SharePoint + MinIO).
    /// Utilisé avant de lancer une ingestion manuelle.
    /// </summary>
    [HttpGet("full")]
    public async Task<IActionResult> GetFullHealth(CancellationToken ct)
    {
        var dbOk = false;
        var fileSourceOk = false;
        var blobOk = false;

        // 1. PostgreSQL
        try
        {
            dbOk = await _db.Database.CanConnectAsync(ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Health check: DB failed");
        }

        // 2. SharePoint Online
        try
        {
            fileSourceOk = await _fileSource.TestConnectionAsync(ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Health check: SharePoint failed");
        }

        // 3. MinIO / Blob Storage
        try
        {
            blobOk = await _blob.HealthCheckAsync(ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Health check: MinIO/Blob failed");
        }

        var allOk = dbOk && fileSourceOk && blobOk;
        var result = new
        {
            status = allOk ? "healthy" : "degraded",
            timestamp = DateTime.UtcNow,
            checks = new
            {
                database = dbOk,
                sharepoint = fileSourceOk,
                blobStorage = blobOk
            }
        };

        return allOk ? Ok(result) : StatusCode(503, result);
    }
}
