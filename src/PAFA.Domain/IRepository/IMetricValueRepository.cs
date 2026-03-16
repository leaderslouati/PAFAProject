using PAFA.Domain.Entities;
using PAFA.Domain.Repositories;

namespace PAFA.Domain.IRepository;

public interface IMetricValueRepository : IBaseRepository<MetricValue>
{
    /// <summary>Insertion en masse après validation.</summary>
    Task AddRangeAsync(IEnumerable<MetricValue> metrics, CancellationToken ct = default);

    /// <summary>
    /// Retourne les lignes EAV filtrées.
    /// Pour obtenir une vue pivotée (une ligne par shipper), utiliser
    /// PivotByShipper() sur le résultat dans le handler.
    /// </summary>
    Task<List<MetricValue>> GetFilteredAsync(
        int? year,
        int? month,
        string? metricKey = null,
        string? shipperShortCode = null,
        CancellationToken ct = default);

    /// <summary>Liste des périodes disponibles pour le sélecteur de filtre.</summary>
    Task<List<DateOnly>> GetDistinctPeriodsAsync(CancellationToken ct = default);
}