

using PAFA.Domain.Entities;
using PAFA.Domain.Repositories;
using PAFA.Infrastructure.EfContexts;
using System.Data.Entity;

namespace PAFA.Infrastructure.Repositories
{
    public class IngestedFileRepository : BaseRepository<IngestedFile>, IIngestedFileRepository
    {
        public IngestedFileRepository(PafaDbContext dbContext) : base(dbContext) { }

        public async Task<bool> ExistsAsync(string fileName, CancellationToken cancellationToken = default)
        {
            return await _dbContext.IngestedFiles.AnyAsync(x => x.FileName == fileName, cancellationToken);
        }
    }
}