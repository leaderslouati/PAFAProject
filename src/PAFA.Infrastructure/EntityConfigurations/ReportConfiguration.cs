using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PAFA.Domain.Entities;
using PAFA.Domain.Enums;

namespace PAFA.Infrastructure.EntityConfigurations ; 
public class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> b)
    {
        b.ToTable("Report", "dbo");

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        b.Property(x => x.Title).IsRequired().HasMaxLength(500);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).HasDefaultValue(ReportStatus.Pending);
        b.Property(x => x.FilePath_PDF).HasMaxLength(1000);
        b.Property(x => x.FilePath_Excel).HasMaxLength(1000);
        b.Property(x => x.FilePath_PPTX).HasMaxLength(1000);
        b.Property(x => x.CommentaryBy).HasMaxLength(200);

        b.Property(x => x.IsDeleted).HasDefaultValue(false);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("NOW() AT TIME ZONE 'UTC'");
        b.Property(x => x.CreatedBy).HasMaxLength(100).HasDefaultValue("SYSTEM");
        b.Property(x => x.UpdatedBy).HasMaxLength(100);

        b.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();

        b.HasQueryFilter(x => !x.IsDeleted);

        // Uniqueness: one report per type + schedule + period
        b.HasIndex(x => new { x.ReportTypeId, x.ScheduleNumber, x.PeriodYear, x.PeriodMonth })
         .IsUnique()
         .HasDatabaseName("UK_Report_Type_Schedule_Period");

        b.HasIndex(x => new { x.PeriodYear, x.PeriodMonth, x.ReportTypeId })
         .HasDatabaseName("IX_Report_Period");

        b.HasIndex(x => new { x.Status, x.GeneratedAt })
         .HasDatabaseName("IX_Report_Status");

        // Report → ReportType (N:1)
        b.HasOne(x => x.ReportType)
         .WithMany(rt => rt.Reports)
         .HasForeignKey(x => x.ReportTypeId)
         .OnDelete(DeleteBehavior.Restrict)
         .HasConstraintName("FK_Report_ReportType");
    }
}