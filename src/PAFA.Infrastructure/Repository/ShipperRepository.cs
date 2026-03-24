using Microsoft.EntityFrameworkCore;
using PAFA.Domain.Entities.Referential;
using PAFA.Domain.IRepository;
using PAFA.Infrastructure.Persistence;

namespace PAFA.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for Shipper entity.
/// </summary>
public class ShipperRepository(PafaDbContext ctx)
    : BaseRepository<Shipper>(ctx), IShipperRepository
{
    public async Task<IReadOnlyList<Shipper>> GetActiveShippersAsync(CancellationToken ct = default)
        => await _ctx.Shippers.Where(s => s.IsActive).ToListAsync(ct);

    public Task<Shipper?> GetByShortCodeAsync(string ssc, CancellationToken ct = default)
        => _ctx.Shippers.FirstOrDefaultAsync(s => s.ShortCode == ssc, ct);
}
