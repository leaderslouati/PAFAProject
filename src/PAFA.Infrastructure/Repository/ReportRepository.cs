using Microsoft.EntityFrameworkCore;
using PAFA.Domain.Entities;
using PAFA.Domain.IRepository;
using PAFA.Domain.Repositories;
using PAFA.Infrastructure.Data;

namespace PAFA.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for Report entity.
/// </summary>
public class ReportRepository : BaseRepository<Report>, IReportRepository
{
    public ReportRepository(PafaDbContext dbContext) : base(dbContext) { }
    
    // Add domain-specific methods as needed
}
