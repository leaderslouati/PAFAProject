using Microsoft.EntityFrameworkCore;
using PAFA.Domain.Entities;
using PAFA.Domain.IRepository;
using PAFA.Domain.Repositories;
using PAFA.Infrastructure.Data;

namespace PAFA.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for Shipper entity.
/// </summary>
public class ShipperRepository : BaseRepository<Shipper>, IShipperRepository
{
    public ShipperRepository(PafaDbContext dbContext) : base(dbContext) { }

    public async Task<IReadOnlyList<Shipper>> GetActiveShippersAsync(CancellationToken ct = default)
    {
        return await _dbContext.Shippers
            .Where(s => s.IsActive && !s.IsDeleted)
            .ToListAsync(ct);
    }

    public async Task<Shipper?> GetByShortCodeAsync(string shortCode, CancellationToken ct = default)
    {
        return await _dbContext.Shippers
            .FirstOrDefaultAsync(s => s.ShortCode == shortCode && !s.IsDeleted, ct);
    }
}
