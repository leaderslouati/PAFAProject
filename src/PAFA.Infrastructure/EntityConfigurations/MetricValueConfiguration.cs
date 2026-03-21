using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PAFA.Domain.Entities;

namespace PAFA.Infrastructure.Persistence.Configurations;

public class MetricValueConfiguration : IEntityTypeConfiguration<MetricValue>
{
    public void Configure(EntityTypeBuilder<MetricValue> b)
    {
        b.ToTable("metric_values");

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        b.Property(x => x.ReportingPeriod).IsRequired().HasColumnType("date");
        b.Property(x => x.ShipperShortCode).IsRequired().HasMaxLength(10);
        b.Property(x => x.MetricKey).IsRequired().HasMaxLength(60);
        b.Property(x => x.Value).HasColumnType("numeric(12,4)");
        b.Property(x => x.ProductClassCode)
                .HasColumnName("product_class_code")
                .HasMaxLength(10);

        // ── Contrainte unicité : évite les doublons à l'import ──────
        // Un seul enregistrement par shipper / période / métrique / fichier
        b.HasIndex(x => new {
            x.IngestionFileId,
            x.ShipperShortCode,
            x.ReportingPeriod,
            x.MetricKey
        })
            .IsUnique()
            .HasDatabaseName("ix_mv_unique");

        // ── Index pour requêtes Power BI ────────────────────────────
        b.HasIndex(x => x.ReportingPeriod).HasDatabaseName("ix_mv_period");
        b.HasIndex(x => x.ShipperShortCode).HasDatabaseName("ix_mv_ssc");
        b.HasIndex(x => x.MetricKey).HasDatabaseName("ix_mv_metric_key");
        b.HasIndex(x => new { x.ReportingPeriod, x.MetricKey })
            .HasDatabaseName("ix_mv_period_key");
        // ← NOUVEAU index pour filtrage Power BI
        b.HasIndex(x => x.ProductClassCode)
               .HasDatabaseName("ix_mv_product_class");
        // ── Relation FK → IngestionFile ─────────────────────────────
        b.HasOne(x => x.IngestionFile)
            .WithMany(x => x.MetricValues)
            .HasForeignKey(x => x.IngestionFileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}