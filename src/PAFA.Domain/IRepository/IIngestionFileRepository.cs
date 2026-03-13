using PAFA.Domain.Entities;
using PAFA.Domain.Repositories;

namespace PAFA.Domain.IRepository;

/// <summary>
/// Extended repository for IngestionFile with validation error support.
/// </summary>
public interface IIngestionFileRepository : IBaseRepository<IngestionFile>
{
    /// <summary>
    /// Check if a file with the given name already exists.
    /// </summary>
    Task<bool> ExistsAsync(string fileName, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Add multiple validation errors for a specific ingestion file.
    /// </summary>
    Task AddValidationErrorsAsync(Guid fileId, IEnumerable<ValidationError> errors, CancellationToken ct = default);
    
    /// <summary>
    /// Get ingestion files for a specific job.
    /// </summary>
    Task<IReadOnlyList<IngestionFile>> GetByJobIdAsync(Guid jobId, CancellationToken ct = default);
}
