using PAFA.Domain.Entities;
using PAFA.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using PAFA.Infrastructure.Data;
using PAFA.Domain.IRepository;

namespace PAFA.Infrastructure.Repositories
{
    public class IngestedFileRepository : BaseRepository<IngestionFile>, IIngestionFileRepository
    {
        public IngestedFileRepository(PafaDbContext dbContext) : base(dbContext) { }

        public async Task<bool> ExistsAsync(string fileName, CancellationToken cancellationToken = default)
        {
            return await _dbContext.IngestionFiles.AnyAsync(x => x.FileName == fileName, cancellationToken);
        }

        public async Task AddValidationErrorsAsync(Guid fileId, IEnumerable<ValidationError> errors, CancellationToken ct = default)
        {
            foreach (var error in errors)
            {
                error.IngestionFileId = fileId;
            }
            await _dbContext.ValidationErrors.AddRangeAsync(errors, ct);
        }

        public async Task<IReadOnlyList<IngestionFile>> GetByJobIdAsync(Guid jobId, CancellationToken ct = default)
        {
            return await _dbContext.IngestionFiles
                .Where(f => f.IngestionJobId == jobId)
                .ToListAsync(ct);
        }
    }
}