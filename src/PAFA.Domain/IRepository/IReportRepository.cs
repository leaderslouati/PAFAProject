using PAFA.Domain.Entities;
using PAFA.Domain.Repositories;

namespace PAFA.Domain.IRepository;

/// <summary>
/// Repository for Report entity.
/// </summary>
public interface IReportRepository : IBaseRepository<Report>
{
    /// <summary>
    /// Returns all reports for a given period, ordered by ScheduleNumber.
    /// </summary>
    Task<IReadOnlyList<Report>> GetByPeriodAsync(DateOnly reportingPeriod, CancellationToken ct = default);

    /// <summary>
    /// Returns a single report by ID (non-deleted).
    /// </summary>
    Task<Report?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
