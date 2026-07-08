using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PAFA.Domain.Entities;
using PAFA.Domain.Enums;

namespace PAFA.Infrastructure.EntityConfigurations;

public class IngestionFileConfiguration : IEntityTypeConfiguration<IngestionFile>
{
    public void Configure(EntityTypeBuilder<IngestionFile> builder)
    {
        builder.ToTable("ingestion_files");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
               .HasColumnName("id")
               .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.IngestionJobId)
               .HasColumnName("ingestion_job_id")
               .IsRequired();

        builder.Property(x => x.FileName)
               .HasColumnName("file_name")
               .HasMaxLength(500)
               .IsRequired();

        builder.Property(x => x.SourceSystem)
               .HasColumnName("source_system")
               .HasMaxLength(20)
               .IsRequired();

        builder.Property(x => x.FileType)
               .HasColumnName("file_type")
               .HasConversion<string>()
               .HasMaxLength(10);

        builder.Property(x => x.FileSizeBytes)
               .HasColumnName("file_size_bytes");

        builder.Property(x => x.BlobPath)
               .HasColumnName("blob_path")
               .HasMaxLength(1000);

        builder.Property(x => x.FileHash)
               .HasColumnName("file_hash")
               .HasMaxLength(64);

        builder.Property(x => x.Status)
               .HasColumnName("status")
               .HasConversion<string>()
               .HasMaxLength(30)
               .HasDefaultValue(IngestionFileStatus.Downloaded);

        builder.Property(x => x.ValidationStatus)
               .HasColumnName("validation_status")
               .HasConversion<string>()
               .HasMaxLength(30)
               .HasDefaultValue(ValidationStatus.Valid);

        builder.Property(x => x.RowsRead)
               .HasColumnName("rows_read");

        builder.Property(x => x.RowsValid)
               .HasColumnName("rows_valid");

        builder.Property(x => x.RowsRejected)
               .HasColumnName("rows_rejected");

        builder.Property(x => x.ErrorCount)
               .HasColumnName("error_count")
               .HasDefaultValue(0);

        builder.Property(x => x.DownloadedAt)
               .HasColumnName("downloaded_at");

        builder.Property(x => x.ProcessedAt)
               .HasColumnName("processed_at");

        builder.Property(x => x.LastModifiedRemote)
               .HasColumnName("last_modified_remote");

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

        builder.HasIndex(x => x.FileHash)
               .HasDatabaseName("ix_file_hash");

        builder.HasIndex(x => x.IngestionJobId)
               .HasDatabaseName("ix_file_job_id");

        builder.HasIndex(x => x.Status)
               .HasDatabaseName("ix_file_status");

        // FK Relationships
        builder.HasOne(x => x.IngestionJob)
               .WithMany(x => x.IngestionFiles)
               .HasForeignKey(x => x.IngestionJobId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.ValidationErrors)
               .WithOne(x => x.IngestionFile)
               .HasForeignKey(x => x.IngestionFileId)
               .OnDelete(DeleteBehavior.Cascade);


        builder.HasMany(x => x.MetricValues)
               .WithOne(x => x.IngestionFile)
               .HasForeignKey(x => x.IngestionFileId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}