using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PAFA.Domain.Entities;

namespace PAFA.Infrastructure.EntityConfigurations ; 
// ════════════════════════════════════════════════════════════════════════
//  VALIDATION ERROR
// ════════════════════════════════════════════════════════════════════════
public class ValidationErrorConfiguration : IEntityTypeConfiguration<ValidationError>
{
    public void Configure(EntityTypeBuilder<ValidationError> b)
    {
        b.ToTable("ValidationError", "etl");

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).UseIdentityColumn();

        b.Property(x => x.ErrorCode).IsRequired().HasMaxLength(50);
        b.Property(x => x.ErrorMessage).IsRequired().HasMaxLength(1000);
        b.Property(x => x.ColumnName).HasMaxLength(200);
        b.Property(x => x.OriginalValue).HasMaxLength(500);
        b.Property(x => x.Severity).HasMaxLength(10).HasDefaultValue("ERROR");
        b.Property(x => x.CreatedAt).HasDefaultValueSql("NOW() AT TIME ZONE 'UTC'");

        b.HasIndex(x => new { x.IngestionFileId, x.Severity })
         .HasDatabaseName("IX_ValidationError_File_Severity");

        // ValidationError → IngestionFile (N:1)
        b.HasOne(x => x.IngestionFile)
         .WithMany(f => f.ValidationErrors)
         .HasForeignKey(x => x.IngestionFileId)
         .OnDelete(DeleteBehavior.Cascade)
         .HasConstraintName("FK_ValidationError_IngestionFile");
    }
}