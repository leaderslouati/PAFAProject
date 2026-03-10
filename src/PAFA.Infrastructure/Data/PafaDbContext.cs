using Microsoft.EntityFrameworkCore;
using PAFA.Domain.Entities;
using PAFA.Domain.Entities.ETL;
using PAFA.Infrastructure.EntityConfigurations;

namespace PAFA.Infrastructure.Data;

/// <summary>
/// Main DbContext for the PAFA application.
/// Covers 4 PostgreSQL schemas: dbo, etl, security, audit.
/// </summary>
public class PafaDbContext : DbContext
{
    public PafaDbContext(DbContextOptions<PafaDbContext> options) : base(options) { }

    // ── Schema DBO — Business Entities ──────────────────────────────────
    public DbSet<Shipper>               Shippers              { get; set; }
    public DbSet<ShipperAlias>          ShipperAliases        { get; set; }
    public DbSet<ProductClass>          ProductClasses        { get; set; }
    public DbSet<ShipperProductClass>   ShipperProductClasses { get; set; }
    public DbSet<ReportType>            ReportTypes           { get; set; }
    public DbSet<Report>                Reports               { get; set; }

    // ── Schema ETL — Ingestion Pipeline ─────────────────────────────────
    public DbSet<IngestionJob>    IngestionJobs    { get; set; }
    public DbSet<IngestionFile>   IngestionFiles   { get; set; }
    public DbSet<ValidationError> ValidationErrors { get; set; }



    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all configurations from the same assembly
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ShipperConfiguration).Assembly
        );

        // Create PostgreSQL schemas
        modelBuilder.HasDefaultSchema("public");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            // Fallback for CLI tools (migrations)
            optionsBuilder.UseNpgsql(
                "Host=localhost;Database=PAFA_POC;Username=postgres;Password=yourpassword;",
                sql =>
                {
                    sql.MigrationsHistoryTable("__EFMigrationsHistory", "public");
                    sql.EnableRetryOnFailure(3);
                    sql.CommandTimeout(120);
                }
            );
        }
    }
}