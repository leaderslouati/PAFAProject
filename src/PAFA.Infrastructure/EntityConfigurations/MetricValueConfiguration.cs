using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PAFA.Domain.Entities;

namespace PAFA.Infrastructure.EntityConfigurations;

public class MetricValueConfiguration : IEntityTypeConfiguration<MetricValue>
{
    public void Configure(EntityTypeBuilder<MetricValue> builder)
    {
        builder.ToTable("metric_values");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
               .HasColumnName("id")
               .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.ReportingPeriod)
               .HasColumnName("reporting_period")
               .HasColumnType("date")
               .IsRequired();

        builder.Property(x => x.ShipperId)
               .HasColumnName("shipper_id");

        builder.Property(x => x.ShipperShortCode)
               .HasColumnName("shipper_short_code")
               .HasMaxLength(50)
               .IsRequired();

        builder.Property(x => x.MetricKey)
               .HasColumnName("metric_key")
               .HasMaxLength(50)
               .IsRequired();

        builder.Property(x => x.Value)
               .HasColumnName("value")
               .HasColumnType("numeric(18,6)");

        builder.Property(x => x.TextValue)
               .HasColumnName("text_value");

        builder.Property(x => x.ProductClassCode)
               .HasColumnName("product_class_code")
               .HasMaxLength(10);

        builder.Property(x => x.IngestionFileId)
               .HasColumnName("ingestion_file_id")
               .IsRequired();

        builder.Property(x => x.CreatedAt)
               .HasColumnName("created_at")
               .HasDefaultValueSql("now()");

        builder.Property(x => x.CreatedBy)
               .HasColumnName("created_by")
               .HasMaxLength(100);

        builder.Property(x => x.UpdatedAt)
               .HasColumnName("updated_at");

        builder.Property(x => x.UpdatedBy)
               .HasColumnName("updated_by")
               .HasMaxLength(100);

        builder.Property(x => x.IsDeleted)
               .HasColumnName("is_deleted")
               .HasDefaultValue(false);

        builder.Property(x => x.RowVersion)
               .HasColumnName("row_version")
               .IsConcurrencyToken()
               .IsRequired(false);

        builder.HasIndex(x => x.ShipperId)
               .HasDatabaseName("ix_metric_values_shipper_id");

        builder.HasIndex(x => new { x.ReportingPeriod, x.ShipperShortCode, x.MetricKey })
               .HasDatabaseName("ix_metric_values_period_shipper_key");

        builder.HasIndex(x => x.ReportingPeriod)
               .HasDatabaseName("ix_metric_values_period");

        builder.HasIndex(x => x.MetricKey)
               .HasDatabaseName("ix_metric_values_key");

        // FK Relationships
        builder.HasOne(x => x.Shipper)
               .WithMany(x => x.MetricValues)
               .HasForeignKey(x => x.ShipperId)
               .OnDelete(DeleteBehavior.SetNull)
               .IsRequired(false);

        builder.HasOne(x => x.IngestionFile)
               .WithMany(x => x.MetricValues)
               .HasForeignKey(x => x.IngestionFileId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}