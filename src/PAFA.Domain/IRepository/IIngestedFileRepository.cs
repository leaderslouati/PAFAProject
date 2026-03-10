using PAFA.Domain.Entities;

namespace PAFA.Domain.Repositories
{
    public interface IIngestedFileRepository : IBaseRepository<IngestedFile>
    {
        // Méthodes spécifiques au domaine si besoin plus tard
        Task<bool> ExistsAsync(string fileName, CancellationToken cancellationToken = default);
    }
}