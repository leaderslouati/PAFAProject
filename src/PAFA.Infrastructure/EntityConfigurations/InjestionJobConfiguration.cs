using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PAFA.Domain.Entities;
using PAFA.Domain.Enums;

namespace PAFA.Infrastructure.Persistence.Configurations;

public class IngestionJobConfiguration : IEntityTypeConfiguration<IngestionJob>
{
    public void Configure(EntityTypeBuilder<IngestionJob> b)
    {
        b.ToTable("ingestion_jobs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.JobName).IsRequired().HasMaxLength(100);
        b.Property(x => x.ReportingPeriod).IsRequired().HasColumnType("date");
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.TriggeredBy).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.ErrorSummary).HasMaxLength(2000);
        b.Property(x => x.CorrelationId).IsRequired(false);
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);
        b.Property(x => x.RowVersion)
            .IsRowVersion()
            .IsRequired(false);
        b.HasIndex(x => x.ReportingPeriod).HasDatabaseName("ix_job_period");
        b.HasIndex(x => x.Status).HasDatabaseName("ix_job_status");
        b.HasOne(x => x.ParentJob).WithMany(x => x.RetryJobs)
            .HasForeignKey(x => x.ParentJobId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
        b.HasMany(x => x.IngestionFiles).WithOne(x => x.IngestionJob)
            .HasForeignKey(x => x.IngestionJobId).OnDelete(DeleteBehavior.Cascade);
    }
}