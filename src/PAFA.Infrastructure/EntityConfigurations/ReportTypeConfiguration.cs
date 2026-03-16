using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PAFA.Domain.Entities;
using PAFA.Domain.Enums;

namespace PAFA.Infrastructure.EntityConfigurations;  

public class ReportTypeConfiguration : IEntityTypeConfiguration<ReportType>
{
    public void Configure(EntityTypeBuilder<ReportType> b)
    {
        b.ToTable("report_types");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Code).IsRequired().HasMaxLength(10);
        b.HasIndex(x => x.Code).IsUnique().HasDatabaseName("ix_reporttype_code");
        b.Property(x => x.ScheduleRef).HasMaxLength(20);
        b.Property(x => x.Label).HasMaxLength(200);
        b.Property(x => x.Audience).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);
        b.Property(x => x.RowVersion).IsConcurrencyToken().IsRequired(false);

        b.HasData(
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