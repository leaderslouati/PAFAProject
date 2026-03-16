// ════════════════════════════════════════════════════════════
// PAFA.Infrastructure/Persistence/PafaDbContext.cs
// ════════════════════════════════════════════════════════════
using Microsoft.EntityFrameworkCore;
using PAFA.Domain.Entities;

namespace PAFA.Infrastructure.Persistence;

public class PafaDbContext(DbContextOptions<PafaDbContext> options) : DbContext(options)
{
    public DbSet<IngestionJob> IngestionJobs => Set<IngestionJob>();
    public DbSet<IngestionFile> IngestionFiles => Set<IngestionFile>();
    public DbSet<ValidationError> ValidationErrors => Set<ValidationError>();
    public DbSet<MetricValue> MetricValues => Set<MetricValue>();
    public DbSet<Shipper> Shippers => Set<Shipper>();
    public DbSet<ProductClass> ProductClasses => Set<ProductClass>();
    public DbSet<ShipperProductClass> ShipperProductClasses => Set<ShipperProductClass>();
    public DbSet<ReportType> ReportTypes => Set<ReportType>();
    public DbSet<Report> Reports => Set<Report>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.ApplyConfigurationsFromAssembly(typeof(PafaDbContext).Assembly);

        // Convention globale : CreatedAt → valeur SQL par défaut (stable, non-dynamique)
        foreach (var entity in mb.Model.GetEntityTypes())
        {
            var prop = entity.FindProperty("CreatedAt");
            if (prop != null && prop.ClrType == typeof(DateTime))
                prop.SetDefaultValueSql("NOW() AT TIME ZONE 'UTC'");
        }

        base.OnModelCreating(mb);
    }
}