using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PAFA.Domain.Entities;
using PAFA.Domain.Enums;

namespace PAFA.Infrastructure.EntityConfigurations;

public class IngestionJobConfiguration : IEntityTypeConfiguration<IngestionJob>
{
    public void Configure(EntityTypeBuilder<IngestionJob> builder)
    {
        builder.ToTable("ingestion_jobs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
               .HasColumnName("id")
               .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.JobName)
               .HasColumnName("job_name")
               .HasMaxLength(100)
               .IsRequired();

        builder.Property(x => x.ReportingPeriod)
               .HasColumnName("reporting_period")
               .HasColumnType("date")
               .IsRequired();

        builder.Property(x => x.Status)
               .HasColumnName("status")
               .HasConversion<string>()
               .HasMaxLength(30)
               .HasDefaultValue("Started");

        builder.Property(x => x.FilesExpected)
               .HasColumnName("files_expected");

        builder.Property(x => x.FilesDownloaded)
               .HasColumnName("files_downloaded")
               .HasDefaultValue(0);

        builder.Property(x => x.FilesProcessed)
               .HasColumnName("files_processed")
               .HasDefaultValue(0);

        builder.Property(x => x.FilesFailed)
               .HasColumnName("files_failed")
               .HasDefaultValue(0);

        builder.Property(x => x.RecordsLoaded)
               .HasColumnName("records_loaded")
               .HasDefaultValue(0);

        builder.Property(x => x.ErrorSummary)
               .HasColumnName("error_summary")
               .HasMaxLength(2000);

        builder.Property(x => x.RetryCount)
               .HasColumnName("retry_count")
               .HasDefaultValue(0);

        builder.Property(x => x.TriggeredBy)
               .HasColumnName("triggered_by")
               .HasConversion<string>()
               .HasMaxLength(20);

        builder.Property(x => x.StartedAt)
               .HasColumnName("started_at")
               .HasDefaultValueSql("now()");

        builder.Property(x => x.CompletedAt)
               .HasColumnName("completed_at");

        builder.Property(x => x.ParentJobId)
               .HasColumnName("parent_job_id");

        builder.Property(x => x.CorrelationId)
               .HasColumnName("correlation_id");

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

        builder.HasIndex(x => x.ReportingPeriod)
               .HasDatabaseName("ix_job_period");

        builder.HasIndex(x => x.Status)
               .HasDatabaseName("ix_job_status");

        builder.HasIndex(x => x.CorrelationId)
               .HasDatabaseName("ix_job_correlation_id");

        // FK Relationships
        builder.HasOne(x => x.ParentJob)
               .WithMany(x => x.RetryJobs)
               .HasForeignKey(x => x.ParentJobId)
               .OnDelete(DeleteBehavior.SetNull)
               .IsRequired(false);

        builder.HasMany(x => x.IngestionFiles)
               .WithOne(x => x.IngestionJob)
               .HasForeignKey(x => x.IngestionJobId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}