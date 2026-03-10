using PAFA.Domain.Entities;
using PAFA.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using PAFA.Infrastructure.Data;

namespace PAFA.Infrastructure.Repositories
{
    public class IngestedFileRepository : BaseRepository<IngestionFile>, IIngestedFileRepository
    {
        public IngestedFileRepository(PafaDbContext dbContext) : base(dbContext) { }

        public async Task<bool> ExistsAsync(string fileName, CancellationToken cancellationToken = default)
        {
            return await _dbContext.IngestionFiles.AnyAsync(x => x.FileName == fileName, cancellationToken);
        }
    }
}