using PAFA.Domain.Entities;
using PAFA.Domain.Entities.Referential;
using PAFA.Domain.Repositories;

namespace PAFA.Domain.IRepository;

/// <summary>
/// Repository for Shipper entity with domain-specific queries.
/// </summary>
public interface IShipperRepository : IBaseRepository<Shipper>
{
    /// <summary>
    /// Get all active shippers with their aliases.
    /// </summary>
    Task<IReadOnlyList<Shipper>> GetActiveShippersAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Find shipper by short code (SSC).
    /// </summary>
    Task<Shipper?> GetByShortCodeAsync(string shortCode, CancellationToken ct = default);

    /// <summary>
    /// Upsert a list of shippers: inserts new ones and updates existing records
    /// matched by <see cref="Shipper.ShortCode"/>.
    /// Returns (inserted, updated) counts.
    /// </summary>
    Task<(int Inserted, int Updated)> UpsertShippersAsync(
        IEnumerable<Shipper> shippers, CancellationToken ct = default);
}
