using Microsoft.EntityFrameworkCore;
using PAFA.Domain.Entities.Authentication;
using PAFA.Domain.IRepository;
using PAFA.Infrastructure.Persistence;

namespace PAFA.Infrastructure.Repositories;

public class PafaUserRepository(PafaDbContext ctx) : IPafaUserRepository
{
    public async Task<PafaUser?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await ctx.PafaUsers
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<PafaUser?> GetByEmailAsync(string email, CancellationToken ct = default)
        => await ctx.PafaUsers
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Email == email, ct);

    public async Task<bool> EmailExistsAsync(string email, CancellationToken ct = default)
        => await ctx.PafaUsers.AnyAsync(u => u.Email == email, ct);

    public async Task AddAsync(PafaUser user, CancellationToken ct = default)
        => await ctx.PafaUsers.AddAsync(user, ct);
}
