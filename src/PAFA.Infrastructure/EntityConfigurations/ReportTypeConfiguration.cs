using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PAFA.Domain.Entities;

namespace PAFA.Infrastructure.EntityConfigurations ; 
public class ReportTypeConfiguration : IEntityTypeConfiguration<ReportType>
{
    public void Configure(EntityTypeBuilder<ReportType> b)
    {
        b.ToTable("ReportType", "dbo");

        b.HasKey(x => x.Id);

        b.Property(x => x.Code).IsRequired().HasMaxLength(10);
        b.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UK_ReportType_Code");

        b.Property(x => x.ScheduleRef).IsRequired().HasMaxLength(20);
        b.Property(x => x.Label).IsRequired().HasMaxLength(200);
        b.Property(x => x.IsAnonymised).HasDefaultValue(true);
        b.Property(x => x.ReportCount).HasDefaultValue(0);
        b.Property(x => x.IsActive).HasDefaultValue(true);

        // Seed data
        b.HasData(
            new ReportType { Id=1, Code="SCH2A", ScheduleRef="Schedule 2A", Label="Industry Peer Comparison View – Anonymised",     IsAnonymised=true,  ReportCount=19 },
            new ReportType { Id=2, Code="SCH2B", ScheduleRef="Schedule 2B", Label="Performance Assurance Committee View – Full",   IsAnonymised=false,  ReportCount=22 }
        );
    }
}