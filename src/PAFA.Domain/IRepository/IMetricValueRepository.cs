using PAFA.Domain.Entities;
using PAFA.Domain.Repositories;

namespace PAFA.Domain.IRepository
{
    public interface IMetricValueRepository : IBaseRepository<MetricValue>
    {
        // On peut ajouter des requêtes spécifiques ici plus tard (ex: pour PowerBI)
        Task AddRangeAsync(IEnumerable<MetricValue> metrics, CancellationToken cancellationToken);
    }
}
