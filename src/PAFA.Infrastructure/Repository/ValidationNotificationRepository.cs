using PAFA.Domain.Entities;
using PAFA.Domain.IRepository;
using PAFA.Infrastructure.Persistence;

namespace PAFA.Infrastructure.Repository;

public class ValidationNotificationRepository(PafaDbContext ctx)
    : IValidationNotificationRepository
{
    public async Task AddAsync(ValidationNotification notification, CancellationToken ct = default)
    {
        await ctx.ValidationNotifications.AddAsync(notification, ct);
    }
}
