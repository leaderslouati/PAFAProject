// ════════════════════════════════════════════════════════════
// PAFA.Infrastructure/Persistence/PafaDbContext.cs
// ════════════════════════════════════════════════════════════
using Microsoft.EntityFrameworkCore;
using PAFA.Domain.Entities;
using PAFA.Infrastructure.EntityConfigurations;
using PAFA.Infrastructure.Persistence.Configurations;

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

    public DbSet<DimCalendar> DimCalendars { get; set; }
    public DbSet<FactReadPerformance> FactReadPerformances { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new IngestionJobConfiguration());
        modelBuilder.ApplyConfiguration(new IngestionFileConfiguration());
        modelBuilder.ApplyConfiguration(new MetricValueConfiguration());
        modelBuilder.ApplyConfiguration(new ShipperConfiguration());
        modelBuilder.ApplyConfiguration(new ProductClassConfiguration());
        modelBuilder.ApplyConfiguration(new ShipperProductClassConfiguration());
        modelBuilder.ApplyConfiguration(new ValidationErrorConfiguration());
        modelBuilder.ApplyConfiguration(new ReportConfiguration());
        modelBuilder.ApplyConfiguration(new ReportTypeConfiguration());

        // Power BI configurations
        modelBuilder.ApplyConfiguration(new DimCalendarConfiguration());
        modelBuilder.ApplyConfiguration(new FactReadPerformanceConfiguration());
    }
}