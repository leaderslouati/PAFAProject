// ═══════════════════════════════════════════════════════════
// PAFA.Infrastructure/EntityConfigurations/SupplyPointSnapshotConfiguration.cs
//
// MANQUANT — À CRÉER
// Configuration EF Core pour la table supply_point_snapshots.
// Table volumineuse (grain journalier) — bien indexée.
// ═══════════════════════════════════════════════════════════
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PAFA.Domain.Entities;

namespace PAFA.Infrastructure.EntityConfigurations;

public class SupplyPointSnapshotConfiguration : IEntityTypeConfiguration<SupplyPointSnapshot>
{
    public void Configure(EntityTypeBuilder<SupplyPointSnapshot> b)
    {
        b.ToTable("supply_point_snapshots");

        b.HasKey(x => x.Id);

        b.Property(x => x.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        b.Property(x => x.GasDay)
            .HasColumnType("date")
            .IsRequired();

        b.Property(x => x.ClassId);

        b.Property(x => x.EucCode)
            .HasMaxLength(20);

        b.Property(x => x.LdzCode)
            .HasMaxLength(10);

        b.Property(x => x.MprnCount)
            .IsRequired()
            .HasDefaultValue(0);

        b.Property(x => x.AqRoll)
            .HasColumnType("bigint")
            .IsRequired();

        // ── FK → Shipper ──────────────────────────────────────────────────────
        b.HasOne(x => x.Shipper)
            .WithMany()
            .HasForeignKey(x => x.ShipperId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Index principal pour les requêtes de reporting ────────────────────
        b.HasIndex(x => new { x.GasDay, x.ShipperId, x.ClassId, x.EucCode })
            .HasDatabaseName("IX_snapshot_GasDay_Shipper_Class_EUC");

        // Index secondaire pour filtrer par mois (GasDay range scan)
        b.HasIndex(x => x.GasDay)
            .HasDatabaseName("IX_snapshot_GasDay");
    }
}
