using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PAFA.Domain.Entities;
using PAFA.Domain.Enums;

namespace PAFA.Infrastructure.EntityConfigurations;

public class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> b)
    {
        b.ToTable("reports");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.Title).IsRequired().HasMaxLength(300);
        b.Property(x => x.ReportingPeriod).IsRequired().HasColumnType("date");
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Audience).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.FilePath_PDF).HasMaxLength(1000);
        b.Property(x => x.FilePath_Excel).HasMaxLength(1000);
        b.Property(x => x.FilePath_PPTX).HasMaxLength(1000);
        b.Property(x => x.CommentaryText).HasMaxLength(5000);
        b.Property(x => x.CommentaryBy).HasMaxLength(200);
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);
        b.Property(x => x.RowVersion)
            .IsRowVersion()
            .IsRequired(false);
        b.HasIndex(x => new { x.ReportTypeId, x.ReportingPeriod, x.ScheduleNumber })
            .IsUnique().HasDatabaseName("ix_report_unique");
        b.HasIndex(x => x.Status).HasDatabaseName("ix_report_status");
        b.HasOne(x => x.ReportType).WithMany(x => x.Reports)
            .HasForeignKey(x => x.ReportTypeId).OnDelete(DeleteBehavior.Restrict);
    }
}