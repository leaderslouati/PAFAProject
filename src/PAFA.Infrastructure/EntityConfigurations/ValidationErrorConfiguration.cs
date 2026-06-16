using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PAFA.Domain.Entities;

namespace PAFA.Infrastructure.EntityConfigurations;

public class ValidationErrorConfiguration : IEntityTypeConfiguration<ValidationError>
{
    public void Configure(EntityTypeBuilder<ValidationError> builder)
    {
        builder.ToTable("validation_errors");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
               .HasColumnName("id")
               .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.IngestionFileId)
               .HasColumnName("ingestion_file_id")
               .IsRequired();

        builder.Property(x => x.LineNumber)
               .HasColumnName("line_number");

        builder.Property(x => x.ColumnName)
               .HasColumnName("column_name")
               .HasMaxLength(100);

        builder.Property(x => x.ErrorCode)
               .HasColumnName("error_code")
               .HasMaxLength(50)
               .IsRequired();

        builder.Property(x => x.ErrorMessage)
               .HasColumnName("error_message")
               .HasMaxLength(1000)
               .IsRequired();

        builder.Property(x => x.OriginalValue)
               .HasColumnName("original_value")
               .HasMaxLength(500);

        builder.Property(x => x.Severity)
               .HasColumnName("severity")
               .HasMaxLength(20)
               .HasDefaultValue("ERROR");

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
               .HasDatabaseName("ix_ve_file_id");

        builder.HasIndex(x => x.ErrorCode)
               .HasDatabaseName("ix_ve_error_code");

        // FK Relationship
        builder.HasOne(x => x.IngestionFile)
               .WithMany(x => x.ValidationErrors)
               .HasForeignKey(x => x.IngestionFileId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}