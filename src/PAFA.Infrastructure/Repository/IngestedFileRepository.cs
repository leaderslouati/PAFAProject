using Microsoft.EntityFrameworkCore;
using PAFA.Domain.Entities;
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
}