using PAFA.Domain.Entities;
using PAFA.Domain.IRepository;
using PAFA.Infrastructure.Persistence;

namespace PAFA.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for Report entity.
/// </summary>
public class ReportRepository(PafaDbContext ctx)
    : BaseRepository<Report>(ctx), IReportRepository
{ }
