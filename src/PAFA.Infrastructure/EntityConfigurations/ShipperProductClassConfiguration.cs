using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Graph.Models;
using PAFA.Domain.Entities;
using PAFA.Domain.Entities.Referential;

namespace PAFA.Infrastructure.EntityConfigurations;

public class ShipperProductClassConfiguration : IEntityTypeConfiguration<ShipperProductClass>
{
    public void Configure(EntityTypeBuilder<ShipperProductClass> entity)
    {
        entity.ToTable("shipper_product_classes");
        // Clé composite : ShipperId + ProductClassId + ReportingPeriod
        entity.HasKey(e => new { e.ShipperId, e.ProductClassId, e.ReportingPeriod });

        entity.Property(e => e.EstimatedPct)
              .HasColumnType("numeric(8,4)")
              .HasComment("% lectures estimées (0-100). Source: MetricKey='EstimatedPct'.");

        entity.Property(e => e.CheckReadCountNotCompleted)
              .HasComment("Nb check reads non complétés. >= 0.");

        entity.Property(e => e.ReadPerfPct)
              .HasColumnType("numeric(8,4)")
              .HasComment("% global de performance lecture (0-100).");

        entity.Property(e => e.NoMeterCount)
              .HasComment("Nb SP sans meter enregistré. >= 0.");

        entity.Property(e => e.NoMeterPct)
              .HasColumnType("numeric(8,4)")
              .HasComment("% SP sans meter. PC1/PC2 = 0.");

        entity.HasOne(e => e.Shipper)
              .WithMany(s => s.ProductClasses)
              .HasForeignKey(e => e.ShipperId)
              .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.ProductClass)
              .WithMany(pc => pc.ShipperProductClasses)
              .HasForeignKey(e => e.ProductClassId)
              .OnDelete(DeleteBehavior.Restrict);
    }
}
