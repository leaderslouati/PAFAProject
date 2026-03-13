using Microsoft.EntityFrameworkCore;
using PAFA.Domain.Entities;
using PAFA.Domain.IRepository;
using PAFA.Infrastructure.Data;

namespace PAFA.Infrastructure.Repositories
{
    /// <summary>
    /// Repository implementation for MetricValue entity.
    /// </summary>
    public class MetricValueRepository : BaseRepository<MetricValue>, IMetricValueRepository
    {
        public MetricValueRepository(PafaDbContext dbContext) : base(dbContext) { }

        public async Task AddRangeAsync(IEnumerable<MetricValue> metrics, CancellationToken cancellationToken)
        {
           await _dbContext.Set<MetricValue>().AddRangeAsync(metrics, cancellationToken);
        }
    }
}