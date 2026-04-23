using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PAFA.Domain.Entities;

namespace PAFA.Infrastructure.EntityConfigurations;

public class ValidationNotificationConfiguration : IEntityTypeConfiguration<ValidationNotification>
{
    public void Configure(EntityTypeBuilder<ValidationNotification> b)
    {
        b.ToTable("validation_notifications");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.FileName).IsRequired().HasMaxLength(500);
        b.Property(x => x.ReportingPeriod).IsRequired().HasMaxLength(20);
        b.Property(x => x.SourceSystem).IsRequired().HasMaxLength(20);
        b.Property(x => x.Recipients).IsRequired().HasMaxLength(2000);
        b.Property(x => x.Status).IsRequired().HasMaxLength(10);
        b.Property(x => x.ErrorDetail).HasMaxLength(2000);
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);

        b.HasOne(x => x.IngestionFile)
            .WithMany()
            .HasForeignKey(x => x.IngestionFileId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.IngestionFileId).HasDatabaseName("ix_val_notif_file");
        b.HasIndex(x => x.SentAt).HasDatabaseName("ix_val_notif_sent_at");
    }
}
