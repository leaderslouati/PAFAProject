using PAFA.Domain.Entities;

namespace PAFA.Domain.IRepository;

/// <summary>
/// Persistence contract for <see cref="ValidationNotification"/> audit records.
/// </summary>
public interface IValidationNotificationRepository
{
    Task AddAsync(ValidationNotification notification, CancellationToken ct = default);
}
