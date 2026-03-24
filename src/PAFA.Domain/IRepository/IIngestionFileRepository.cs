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

    /// <summary>
    /// Get all validation errors for a specific file.
    /// </summary>
    Task<IReadOnlyList<ValidationError>> GetValidationErrorsAsync(Guid fileId, CancellationToken ct = default);

    /// <summary>
    /// Returns the set of file names already successfully loaded (Status=Loaded)
    /// for a given reporting period. Used by the cron to skip already-processed files
    /// on resume/retry.
    /// </summary>
    Task<HashSet<string>> GetAlreadyLoadedFileNamesAsync(int year, int month, CancellationToken ct = default);
}
