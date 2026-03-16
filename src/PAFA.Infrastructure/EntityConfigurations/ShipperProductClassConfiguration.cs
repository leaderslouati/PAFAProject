using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PAFA.Domain.Entities;

namespace PAFA.Infrastructure.EntityConfigurations;

public class ShipperProductClassConfiguration : IEntityTypeConfiguration<ShipperProductClass>
{
    public void Configure(EntityTypeBuilder<ShipperProductClass> b)
    {
        b.ToTable("shipper_product_classes");
        b.HasKey(x => new { x.ShipperId, x.ProductClassId, x.ReportingPeriod });
        b.Property(x => x.ReportingPeriod).HasColumnType("date");
        b.Property(x => x.TotalAQ_MWH).HasColumnType("numeric(14,4)");
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);
        b.Property(x => x.RowVersion)
            .IsRowVersion()
            .IsRequired(false)
            .HasDefaultValueSql("decode('', 'hex')");
        b.HasOne(x => x.Shipper).WithMany(x => x.ProductClasses)
            .HasForeignKey(x => x.ShipperId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.ProductClass).WithMany(x => x.ShipperProductClasses)
            .HasForeignKey(x => x.ProductClassId).OnDelete(DeleteBehavior.Restrict);
    }
}
