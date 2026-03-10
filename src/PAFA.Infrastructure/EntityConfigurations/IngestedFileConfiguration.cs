using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PAFA.Domain.Entities;

namespace PAFA.Infrastructure.EntityConfigurations
{
    public class IngestionFileConfiguration : IEntityTypeConfiguration<IngestionFile>
{
    public void Configure(EntityTypeBuilder<IngestionFile> b)
    {
        b.ToTable("IngestionFile", "etl");

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        b.Property(x => x.FileName).IsRequired().HasMaxLength(500);
        b.Property(x => x.SourceSystem).HasMaxLength(10).HasDefaultValue("CDSP");
        b.Property(x => x.FileType).HasConversion<string>().HasMaxLength(10);
        b.Property(x => x.BlobPath).HasMaxLength(1000);
        b.Property(x => x.Checksum).HasMaxLength(64);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.ValidationStatus).HasConversion<string>().HasMaxLength(20);

        b.HasIndex(x => new { x.IngestionJobId, x.Status })
         .HasDatabaseName("IX_IngestionFile_Job_Status");

        // IngestionFile → IngestionJob (N:1)
        b.HasOne(x => x.IngestionJob)
         .WithMany(j => j.Files)
         .HasForeignKey(x => x.IngestionJobId)
         .OnDelete(DeleteBehavior.Cascade)
         .HasConstraintName("FK_IngestionFile_IngestionJob");
    }
}
}