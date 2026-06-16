using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PAFA.Domain.Entities;
using PAFA.Domain.Enums;

namespace PAFA.Infrastructure.EntityConfigurations;

public class ReportTypeConfiguration : IEntityTypeConfiguration<ReportType>
{
    public void Configure(EntityTypeBuilder<ReportType> builder)
    {
        builder.ToTable("report_types");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
               .HasColumnName("id")
               .ValueGeneratedNever();

        builder.Property(x => x.Code)
               .HasColumnName("code")
               .HasMaxLength(10)
               .IsRequired();

        builder.Property(x => x.ScheduleRef)
               .HasColumnName("schedule_ref")
               .HasMaxLength(20);

        builder.Property(x => x.Label)
               .HasColumnName("label")
               .HasMaxLength(200)
               .IsRequired();

        builder.Property(x => x.Audience)
               .HasColumnName("audience")
               .HasConversion<string>()
               .HasMaxLength(20);

        builder.Property(x => x.ReportCount)
               .HasColumnName("report_count")
               .HasDefaultValue(0);

        builder.Property(x => x.IsActive)
               .HasColumnName("is_active")
               .HasDefaultValue(true);

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

        builder.HasIndex(x => x.Code)
               .IsUnique()
               .HasDatabaseName("ix_reporttype_code");

        // Relationships
        builder.HasMany(x => x.Reports)
               .WithOne(x => x.ReportType)
               .HasForeignKey(x => x.ReportTypeId)
               .OnDelete(DeleteBehavior.Restrict);

        // Seed Data
        builder.HasData(
            new ReportType
            {
                Id = 1,
                Code = "SCH2A",
                ScheduleRef = "Schedule 2A",
                Label = "Industry Peer Comparison (Anonymised)",
                Audience = ReportAudience.Industry,
                ReportCount = 19,
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedBy = "SYSTEM"
            },
            new ReportType
            {
                Id = 2,
                Code = "SCH2B",
                ScheduleRef = "Schedule 2B",
                Label = "Performance Assurance Committee (Non-Anonymised)",
                Audience = ReportAudience.PAC,
                ReportCount = 22,
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedBy = "SYSTEM"
            });
    }
}