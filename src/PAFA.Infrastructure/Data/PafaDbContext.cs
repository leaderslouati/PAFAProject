// ════════════════════════════════════════════════════════════
// PAFA.Infrastructure/Persistence/PafaDbContext.cs
// ════════════════════════════════════════════════════════════
using Microsoft.EntityFrameworkCore;
using PAFA.Domain.Entities;

using PAFA.Domain.Entities.Authentication;
using PAFA.Domain.Entities.Referential;
using PAFA.Infrastructure.EntityConfigurations;

namespace PAFA.Infrastructure.Persistence;

public class PafaDbContext(DbContextOptions<PafaDbContext> options) : DbContext(options)
{
    // ── Auth ────────────────────────────────────────────────
    public DbSet<PafaUser> PafaUsers => Set<PafaUser>();
    public DbSet<PafaRole> PafaRoles => Set<PafaRole>();
    public DbSet<PafaUserRole> PafaUserRoles => Set<PafaUserRole>();
    public DbSet<PafaPermission> PafaPermissions => Set<PafaPermission>();
    public DbSet<PafaRolePermission> PafaRolePermissions => Set<PafaRolePermission>();

    // ── Referential ─────────────────────────────────────────
    public DbSet<Shipper> Shippers => Set<Shipper>();
    public DbSet<ProductClass> ProductClasses => Set<ProductClass>();
    public DbSet<ShipperProductClass> ShipperProductClasses => Set<ShipperProductClass>();
    public DbSet<ShipperAlias> ShipperAliases => Set<ShipperAlias>();

    // ── Ingestion ───────────────────────────────────────────
    public DbSet<IngestionJob> IngestionJobs => Set<IngestionJob>();
    public DbSet<IngestionFile> IngestionFiles => Set<IngestionFile>();
    public DbSet<ValidationError> ValidationErrors => Set<ValidationError>();
    public DbSet<ValidationNotification> ValidationNotifications => Set<ValidationNotification>();
    public DbSet<MetricValue> MetricValues => Set<MetricValue>();

    // ── Reporting ────────────────────────────────────────────
    public DbSet<ReportType> ReportTypes => Set<ReportType>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<FactReadPerformance> FactReadPerformances => Set<FactReadPerformance>();       



    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new IngestionJobConfiguration());
        modelBuilder.ApplyConfiguration(new IngestionFileConfiguration());
        modelBuilder.ApplyConfiguration(new MetricValueConfiguration());
        modelBuilder.ApplyConfiguration(new ShipperConfiguration());
        modelBuilder.ApplyConfiguration(new ProductClassConfiguration());
        modelBuilder.ApplyConfiguration(new ShipperProductClassConfiguration());
        modelBuilder.ApplyConfiguration(new ValidationErrorConfiguration());
        modelBuilder.ApplyConfiguration(new ValidationNotificationConfiguration());
        modelBuilder.ApplyConfiguration(new ReportConfiguration());
        modelBuilder.ApplyConfiguration(new ReportTypeConfiguration());
        modelBuilder.ApplyConfiguration(new PafaRoleConfiguration());
        modelBuilder.ApplyConfiguration(new PafaUserConfiguration());
        modelBuilder.ApplyConfiguration(new PafaUserRoleConfiguration());
        modelBuilder.ApplyConfiguration(new ShipperAliasConfiguration());
        modelBuilder.ApplyConfiguration(new PafaPermissionConfiguration());
        modelBuilder.ApplyConfiguration(new PafaRolePermissionConfiguration());

        // Power BI configurations
        modelBuilder.ApplyConfiguration(new FactReadPerformanceConfiguration());
    }
}