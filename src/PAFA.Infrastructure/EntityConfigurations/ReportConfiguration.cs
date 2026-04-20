using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Report = PAFA.Domain.Entities.Report;

namespace PAFA.Infrastructure.EntityConfigurations;

public class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> entity)
    {
        entity.ToTable("reports");
        entity.HasKey(e => e.Id);

        entity.Property(e => e.Title)
              .IsRequired()
              .HasMaxLength(500);

        entity.Property(e => e.ObservationsText)
              .HasColumnType("text")
              .HasComment("Observations mensuelles saisies manuellement par l'analyste PAFA.");

        entity.Property(e => e.ObservationsBy)
              .HasMaxLength(256)
              .HasComment("Identifiant (UPN/email) de l'analyste.");

        entity.Property(e => e.ObservationsUpdatedAt)
              .HasComment("Horodatage UTC de la dernière mise à jour des observations.");

        entity.Property(e => e.IngestionJobId)
              .IsRequired(false);

        entity.HasOne(e => e.ReportType)
              .WithMany(rt => rt.Reports)
              .HasForeignKey(e => e.ReportTypeId)
              .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.IngestionJob)
              .WithMany()
              .HasForeignKey(e => e.IngestionJobId)
              .OnDelete(DeleteBehavior.SetNull)
              .IsRequired(false);

        entity.Property(e => e.Status)
              .HasConversion<int>()          
              .HasColumnType("integer");
        
        entity.Property(e => e.Audience)
              .HasConversion<int>()        
              .HasColumnType("integer");
        entity.Property(e => e.CommentaryText)
      .HasColumnType("text");

        entity.Property(e => e.CommentaryBy)
              .HasMaxLength(200);

        entity.Property(e => e.CreatedBy)
              .HasMaxLength(100);

        entity.Property(e => e.UpdatedBy)
              .HasMaxLength(100);

        entity.Property(e => e.FilePath_PDF)
              .HasMaxLength(1000);

        entity.Property(e => e.FilePath_Excel)
              .HasMaxLength(1000);

        entity.Property(e => e.FilePath_PPTX)
              .HasMaxLength(1000);

        // Index pour requêtes par période de reporting
        entity.HasIndex(e => new { e.ReportingPeriod, e.ReportTypeId })
              .HasDatabaseName("IX_reports_Period_Type");
    }
}