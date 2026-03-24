using PAFA.Domain.Entities.Authentication;

namespace PAFA.Domain.IRepository;

/// <summary>
/// Repository for PafaUser CRUD operations.
/// </summary>
public interface IPafaUserRepository
{
    Task<PafaUser?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PafaUser?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);
    Task AddAsync(PafaUser user, CancellationToken ct = default);
}
