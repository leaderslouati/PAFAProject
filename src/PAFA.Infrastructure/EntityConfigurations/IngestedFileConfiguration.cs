using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PAFA.Domain.Entities;

namespace PAFA.Infrastructure.EntityConfigurations;  

public class IngestionFileConfiguration : IEntityTypeConfiguration<IngestionFile>
{
    public void Configure(EntityTypeBuilder<IngestionFile> b)
    {
        b.ToTable("ingestion_files");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.FileName).IsRequired().HasMaxLength(500);
        b.Property(x => x.SourceSystem).IsRequired().HasMaxLength(20);
        b.Property(x => x.FileType).HasConversion<string>().HasMaxLength(10);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.ValidationStatus).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.BlobPath).HasMaxLength(1000);
        b.Property(x => x.FileHash).HasMaxLength(64);
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);
        b.Property(x => x.RowVersion)
            .IsRowVersion()
            .IsRequired(false)
            .HasDefaultValueSql("decode('', 'hex')");
        b.HasIndex(x => x.FileHash).HasDatabaseName("ix_file_hash");
        b.Property(x => x.LastModifiedRemote);
        b.HasMany(x => x.ValidationErrors).WithOne(x => x.IngestionFile)
            .HasForeignKey(x => x.IngestionFileId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.MetricValues).WithOne(x => x.IngestionFile)
            .HasForeignKey(x => x.IngestionFileId).OnDelete(DeleteBehavior.Cascade);
    }
}