using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Graph.Models;
using PAFA.Domain.Entities;

namespace PAFA.Infrastructure.Persistence.Configurations;

public class MetricValueConfiguration : IEntityTypeConfiguration<MetricValue>
{
    public void Configure(EntityTypeBuilder<MetricValue> entity)
    {
        entity.ToTable("metric_values");

        entity.HasKey(e => e.Id);

        entity.Property(e => e.ShipperShortCode)
              .IsRequired()
              .HasMaxLength(20);

        entity.Property(e => e.MetricKey)
              .IsRequired()
              .HasMaxLength(50);

        entity.Property(e => e.Value)
              .HasColumnType("numeric(18,6)");

        entity.Property(e => e.ProductClassCode)
              .HasMaxLength(10);

        // FK nullable vers Shipper (transition : ShipperShortCode → ShipperId)
        entity.Property(e => e.ShipperId)
              .IsRequired(false);

        entity.HasIndex(e => e.ShipperId)
              .HasDatabaseName("IX_metric_values_ShipperId");

        entity.HasOne(e => e.Shipper)
              .WithMany(s => s.MetricValues)
              .HasForeignKey(e => e.ShipperId)
              .OnDelete(DeleteBehavior.SetNull)
              .IsRequired(false);

        entity.HasOne(e => e.IngestionFile)
              .WithMany(f => f.MetricValues)
              .HasForeignKey(e => e.IngestionFileId)
              .OnDelete(DeleteBehavior.Cascade);

        // Index de performance pour les requêtes par période/shipper/metric
        entity.HasIndex(e => new { e.ReportingPeriod, e.ShipperShortCode, e.MetricKey })
              .HasDatabaseName("IX_metric_values_Period_Shipper_MetricKey");
    }
}