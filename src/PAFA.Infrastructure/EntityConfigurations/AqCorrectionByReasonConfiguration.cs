// ═══════════════════════════════════════════════════════════
// PAFA.Infrastructure/EntityConfigurations/AqCorrectionByReasonConfiguration.cs
//
// MANQUANT — À CRÉER
// Configuration EF Core pour la table aq_corrections_by_reason.
// ═══════════════════════════════════════════════════════════
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PAFA.Domain.Entities;

namespace PAFA.Infrastructure.EntityConfigurations;

public class AqCorrectionByReasonConfiguration : IEntityTypeConfiguration<AqCorrectionByReason>
{
    public void Configure(EntityTypeBuilder<AqCorrectionByReason> b)
    {
        b.ToTable("aq_corrections_by_reason");

        b.HasKey(x => x.Id);

        // UUID généré par la BDD (PostgreSQL)
        b.Property(x => x.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        b.Property(x => x.PeriodId)
            .IsRequired()
            .HasComment("Format YYYYMM — ex. 202603 = Mars 2026");

        b.Property(x => x.MprnCount)
            .IsRequired()
            .HasDefaultValue(0);

        b.Property(x => x.IngestionFileId).IsRequired();

        // ── FK → Shipper (nullable — NULL = Industry Total) ──────────────────
        b.HasOne(x => x.Shipper)
            .WithMany()
            .HasForeignKey(x => x.ShipperId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // ── FK → LookupValue (ReasonCode) ────────────────────────────────────
        b.HasOne(x => x.ReasonCode)
            .WithMany()
            .HasForeignKey(x => x.ReasonCodeLookupId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        // ── Index composite pour les requêtes courantes ───────────────────────
        b.HasIndex(x => new { x.PeriodId, x.ShipperId, x.ReasonCodeLookupId })
            .HasDatabaseName("IX_aq_corrections_Period_Shipper_Reason");

        b.HasIndex(x => x.PeriodId)
            .HasDatabaseName("IX_aq_corrections_PeriodId");
    }
}
