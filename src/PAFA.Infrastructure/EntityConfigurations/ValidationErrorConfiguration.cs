using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PAFA.Domain.Entities;

namespace PAFA.Infrastructure.EntityConfigurations;  

// ════════════════════════════════════════════════════════════════════════
//  VALIDATION ERROR
// ════════════════════════════════════════════════════════════════════════
public class ValidationErrorConfiguration : IEntityTypeConfiguration<ValidationError>
{
    public void Configure(EntityTypeBuilder<ValidationError> b)
    {
        b.ToTable("validation_errors");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.ErrorCode).IsRequired().HasMaxLength(50);
        b.Property(x => x.ErrorMessage).IsRequired().HasMaxLength(1000);
        b.Property(x => x.ColumnName).HasMaxLength(100);
        b.Property(x => x.OriginalValue).HasMaxLength(500);
        b.Property(x => x.Severity).IsRequired().HasMaxLength(10);
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);
        b.Property(x => x.RowVersion)
            .IsRowVersion()
            .IsRequired(false);
        b.HasIndex(x => x.IngestionFileId).HasDatabaseName("ix_valerr_file");
    }
}