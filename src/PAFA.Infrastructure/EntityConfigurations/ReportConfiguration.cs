using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PAFA.Domain.Enums;
using Report = PAFA.Domain.Entities.Report;

namespace PAFA.Infrastructure.EntityConfigurations;

public class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> builder)
    {
        builder.ToTable("reports");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
               .HasColumnName("id")
               .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.ReportTypeId)
               .HasColumnName("report_type_id")
               .IsRequired();

        builder.Property(x => x.ScheduleNumber)
               .HasColumnName("schedule_number")
               .IsRequired();

        builder.Property(x => x.Title)
               .HasColumnName("title")
               .HasMaxLength(500)
               .IsRequired();

        builder.Property(x => x.ReportingPeriod)
               .HasColumnName("reporting_period")
               .HasColumnType("date")
               .IsRequired();

        builder.Property(x => x.Audience)
               .HasColumnName("audience")
               .HasConversion<string>()
               .HasMaxLength(20);

        builder.Property(x => x.Status)
               .HasColumnName("status")
               .HasConversion<string>()
               .HasMaxLength(30)
               .HasDefaultValue("Pending");

        builder.Property(x => x.GeneratedAt)
               .HasColumnName("generated_at");

        builder.Property(x => x.PublishedAt)
               .HasColumnName("published_at");

        builder.Property(x => x.FilePath_PDF)
               .HasColumnName("file_path_pdf")
               .HasMaxLength(1000);

        builder.Property(x => x.FilePath_Excel)
               .HasColumnName("file_path_excel")
               .HasMaxLength(1000);

        builder.Property(x => x.FilePath_PPTX)
               .HasColumnName("file_path_pptx")
               .HasMaxLength(1000);

        builder.Property(x => x.CommentaryText)
               .HasColumnName("commentary_text");

        builder.Property(x => x.CommentaryBy)
               .HasColumnName("commentary_by")
               .HasMaxLength(200);

        builder.Property(x => x.ObservationsText)
               .HasColumnName("observations_text");

        builder.Property(x => x.ObservationsBy)
               .HasColumnName("observations_by")
               .HasMaxLength(256);

        builder.Property(x => x.ObservationsUpdatedAt)
               .HasColumnName("observations_updated_at");

        builder.Property(x => x.IngestionJobId)
               .HasColumnName("ingestion_job_id");

        builder.Property(x => x.IsBaseline)
               .HasColumnName("is_baseline")
               .HasDefaultValue(false);

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

        builder.HasIndex(x => new { x.ReportingPeriod, x.ReportTypeId })
               .HasDatabaseName("ix_reports_period_type");

        builder.HasIndex(x => x.Status)
               .HasDatabaseName("ix_reports_status");

        builder.HasIndex(x => x.Audience)
               .HasDatabaseName("ix_reports_audience");

        // FK Relationships
        builder.HasOne(x => x.ReportType)
               .WithMany(x => x.Reports)
               .HasForeignKey(x => x.ReportTypeId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.IngestionJob)
               .WithMany()
               .HasForeignKey(x => x.IngestionJobId)
               .OnDelete(DeleteBehavior.SetNull)
               .IsRequired(false);
    }
}