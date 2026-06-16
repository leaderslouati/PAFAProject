// ═══════════════════════════════════════════════════════════
// PAFA.Infrastructure/EntityConfigurations/EucBandConfiguration.cs
//
// MANQUANT — À CRÉER
// Configuration EF Core pour la table euc_bands.
// Contient le seed des 9 bandes EUC (données fixes réglementaires).
// ═══════════════════════════════════════════════════════════
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PAFA.Domain.Entities.Referential;

namespace PAFA.Infrastructure.EntityConfigurations;

public class EucBandConfiguration : IEntityTypeConfiguration<EucBand>
{
    public void Configure(EntityTypeBuilder<EucBand> b)
    {
        b.ToTable("euc_bands");

        b.HasKey(x => x.EucCode);

        b.Property(x => x.EucCode)
            .HasMaxLength(10)
            .IsRequired();

        b.Property(x => x.Description)
            .HasMaxLength(200)
            .IsRequired();

        b.Property(x => x.AqThresholdMinKwh)
            .HasColumnType("bigint")
            .IsRequired();

        b.Property(x => x.AqThresholdMaxKwh)
            .HasColumnType("bigint")
            .IsRequired();

        // ── Seed des 9 bandes EUC (seuils réglementaires UNC) ──────────────
        b.HasData(
            new EucBand
            {
                EucCode = "EUC01",
                Description = "< 73,200 kWh/yr",
                AqThresholdMinKwh = 0,
                AqThresholdMaxKwh = 73_199
            },
            new EucBand
            {
                EucCode = "EUC02",
                Description = "73,200 – 293,000 kWh/yr",
                AqThresholdMinKwh = 73_200,
                AqThresholdMaxKwh = 292_999
            },
            new EucBand
            {
                EucCode = "EUC03",
                Description = "293,000 – 732,000 kWh/yr",
                AqThresholdMinKwh = 293_000,
                AqThresholdMaxKwh = 731_999
            },
            new EucBand
            {
                EucCode = "EUC04",
                Description = "732,000 – 2,196,000 kWh/yr",
                AqThresholdMinKwh = 732_000,
                AqThresholdMaxKwh = 2_195_999
            },
            new EucBand
            {
                EucCode = "EUC05",
                Description = "2,196,000 – 7,320,000 kWh/yr",
                AqThresholdMinKwh = 2_196_000,
                AqThresholdMaxKwh = 7_319_999
            },
            new EucBand
            {
                EucCode = "EUC06",
                Description = "7,320,000 – 14,640,000 kWh/yr",
                AqThresholdMinKwh = 7_320_000,
                AqThresholdMaxKwh = 14_639_999
            },
            new EucBand
            {
                EucCode = "EUC07",
                Description = "14,640,000 – 29,280,000 kWh/yr",
                AqThresholdMinKwh = 14_640_000,
                AqThresholdMaxKwh = 29_279_999
            },
            new EucBand
            {
                EucCode = "EUC08",
                Description = "29,280,000 – 58,600,000 kWh/yr",
                AqThresholdMinKwh = 29_280_000,
                AqThresholdMaxKwh = 58_599_999
            },
            new EucBand
            {
                EucCode = "EUC09",
                Description = ">= 58,600,000 kWh/yr",
                AqThresholdMinKwh = 58_600_000,
                AqThresholdMaxKwh = long.MaxValue
            }
        );
    }
}
