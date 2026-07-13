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

    public async Task<(int Inserted, int Updated)> UpsertShippersAsync(
        IEnumerable<Shipper> shippers, CancellationToken ct = default)
    {
        int inserted = 0, updated = 0;

        foreach (var incoming in shippers)
        {
            if (string.IsNullOrWhiteSpace(incoming.ShortCode))
                continue;

            var existing = await _ctx.Shippers
                .FirstOrDefaultAsync(s => s.ShortCode == incoming.ShortCode, ct);

            if (existing is null)
            {
                incoming.Id = Guid.NewGuid();
                await _ctx.Shippers.AddAsync(incoming, ct);
                inserted++;
            }
            else
            {
                existing.Name          = incoming.Name;
                existing.AliasCode     = incoming.AliasCode;
                existing.LegalEntity   = incoming.LegalEntity;
                existing.Email         = incoming.Email;
                existing.IsActive      = incoming.IsActive;
                existing.PortfolioSize = incoming.PortfolioSize;

                if (incoming.MarketEntryDate.HasValue)
                    existing.MarketEntryDate = incoming.MarketEntryDate;
                if (incoming.MarketExitDate.HasValue)
                    existing.MarketExitDate = incoming.MarketExitDate;

                _ctx.Shippers.Update(existing);
                updated++;
            }
        }

        await _ctx.SaveChangesAsync(ct);
        return (inserted, updated);
    }
}
