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

    // NOUVEAU : Référentiel PARR
    public DbSet<LookupValue> LookupValues => Set<LookupValue>();
    public DbSet<EucBand> EucBands => Set<EucBand>();
    public DbSet<ReportDefinition> ReportDefinitions => Set<ReportDefinition>();
    public DbSet<MetricDefinition> MetricDefinitions => Set<MetricDefinition>();

    // ── Ingestion ───────────────────────────────────────────
    public DbSet<IngestionJob> IngestionJobs => Set<IngestionJob>();
    public DbSet<IngestionFile> IngestionFiles => Set<IngestionFile>();
    public DbSet<ValidationError> ValidationErrors => Set<ValidationError>();
    public DbSet<ValidationNotification> ValidationNotifications => Set<ValidationNotification>();
    public DbSet<MetricValue> MetricValues => Set<MetricValue>();

    // ── Reporting & Fact Tables ──────────────────────────────
    public DbSet<ReportType> ReportTypes => Set<ReportType>();
    public DbSet<Report> Reports => Set<Report>();

    // Vues et tables de faits (Power BI)
    public DbSet<FactReadPerformance> FactReadPerformances => Set<FactReadPerformance>();
    public DbSet<AqCorrectionByReason> AqCorrectionsByReason => Set<AqCorrectionByReason>();
    public DbSet<SupplyPointSnapshot> SupplyPointSnapshots => Set<SupplyPointSnapshot>();

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
        modelBuilder.ApplyConfiguration(new PafaPermissionConfiguration());
        modelBuilder.ApplyConfiguration(new PafaRolePermissionConfiguration());
        modelBuilder.ApplyConfiguration(new LookupValueConfiguration());
        // 👇 AJOUTEZ CES 5 LIGNES POUR ÉVITER LES PROCHAINES ERREURS :
        modelBuilder.ApplyConfiguration(new EucBandConfiguration());
        modelBuilder.ApplyConfiguration(new ReportDefinitionConfiguration());
        modelBuilder.ApplyConfiguration(new MetricDefinitionConfiguration());
        modelBuilder.ApplyConfiguration(new AqCorrectionByReasonConfiguration());
        modelBuilder.ApplyConfiguration(new SupplyPointSnapshotConfiguration());
        // Power BI configurations
        modelBuilder.ApplyConfiguration(new FactReadPerformanceConfiguration());
    }
}