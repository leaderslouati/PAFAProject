using DocumentFormat.OpenXml.InkML;
using Microsoft.EntityFrameworkCore;
using PAFA.Domain.Entities;
using PAFA.Domain.Enums;
using PAFA.Domain.IRepository;
using PAFA.Infrastructure.Persistence;

namespace PAFA.Infrastructure.Repositories;  

public class IngestionFileRepository(PafaDbContext ctx)
 : BaseRepository<IngestionFile>(ctx), IIngestionFileRepository
{
    public Task<bool> ExistsAsync(string fileName, CancellationToken ct = default)
        => _ctx.IngestionFiles.AnyAsync(f => f.FileName == fileName, ct);

    public async Task AddValidationErrorsAsync(
        Guid fileId, IEnumerable<ValidationError> errors, CancellationToken ct = default)
    {
        foreach (var e in errors) e.IngestionFileId = fileId;
        await _ctx.ValidationErrors.AddRangeAsync(errors, ct);
    }

    public async Task<IReadOnlyList<IngestionFile>> GetByJobIdAsync(Guid jobId, CancellationToken ct = default)
        => await _ctx.IngestionFiles
            .Where(f => f.IngestionJobId == jobId)
            .Include(f => f.ValidationErrors)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ValidationError>> GetValidationErrorsAsync(
    Guid fileId, CancellationToken ct = default)
    => await _ctx.ValidationErrors
        .Where(e => e.IngestionFileId == fileId)
        .OrderBy(e => e.LineNumber)
        .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<HashSet<string>> GetAlreadyLoadedFileNamesAsync(
        int year, int month, CancellationToken ct = default)
    {
        var period = new DateOnly(year, month, 1);
        var names = await _ctx.IngestionFiles
            .Where(f => f.Status == IngestionFileStatus.Processed
                     && _ctx.IngestionJobs
                            .Any(j => j.Id == f.IngestionJobId
                                   && j.ReportingPeriod == period))
            .Select(f => f.FileName)
            .ToListAsync(ct);
        return new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, DateTime>> GetLoadedFileModificationDatesAsync(
        int year, int month, CancellationToken ct = default)
    {
        var period = new DateOnly(year, month, 1);
        var entries = await _ctx.IngestionFiles
            .Where(f => f.Status == IngestionFileStatus.Processed
                     && f.LastModifiedRemote != null
                     && _ctx.IngestionJobs
                            .Any(j => j.Id == f.IngestionJobId
                                   && j.ReportingPeriod == period))
            .Select(f => new { f.FileName, f.LastModifiedRemote })
            .ToListAsync(ct);
        return entries.ToDictionary(
            e => e.FileName,
            e => e.LastModifiedRemote!.Value,
            StringComparer.OrdinalIgnoreCase);
    }
}