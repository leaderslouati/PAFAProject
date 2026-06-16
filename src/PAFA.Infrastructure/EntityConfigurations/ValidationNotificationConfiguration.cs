using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PAFA.Domain.Entities;

namespace PAFA.Infrastructure.EntityConfigurations;

public class ValidationNotificationConfiguration : IEntityTypeConfiguration<ValidationNotification>
{
    public void Configure(EntityTypeBuilder<ValidationNotification> builder)
    {
        builder.ToTable("validation_notifications");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
               .HasColumnName("id")
               .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.IngestionFileId)
               .HasColumnName("ingestion_file_id")
               .IsRequired();

        builder.Property(x => x.FileName)
               .HasColumnName("file_name")
               .HasMaxLength(500)
               .IsRequired();

        builder.Property(x => x.ReportingPeriod)
               .HasColumnName("reporting_period")
               .HasMaxLength(50)
               .IsRequired();

        builder.Property(x => x.SourceSystem)
               .HasColumnName("source_system")
               .HasMaxLength(20)
               .IsRequired();

        builder.Property(x => x.Recipients)
               .HasColumnName("recipients")
               .HasMaxLength(2000)
               .IsRequired();

        builder.Property(x => x.TotalErrors)
               .HasColumnName("total_errors")
               .IsRequired();

        builder.Property(x => x.SentAt)
               .HasColumnName("sent_at")
               .HasDefaultValueSql("now()");

        builder.Property(x => x.Status)
               .HasColumnName("status")
               .HasMaxLength(30)
               .HasDefaultValue("SENT");

        builder.Property(x => x.ErrorDetail)
               .HasColumnName("error_detail")
               .HasMaxLength(2000);

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

        builder.HasIndex(x => x.IngestionFileId)
               .HasDatabaseName("ix_vn_file_id");

        builder.HasIndex(x => x.Status)
               .HasDatabaseName("ix_vn_status");

        
    }
}
