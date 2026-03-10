using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PAFA.Domain.Entities;

namespace PAFA.Infrastructure.EntityConfigurations ; 
public class ShipperProductClassConfiguration : IEntityTypeConfiguration<ShipperProductClass>
{
    public void Configure(EntityTypeBuilder<ShipperProductClass> b)
    {
        b.ToTable("ShipperProductClass", "dbo");

        // Clé primaire composite
        b.HasKey(x => new { x.ShipperId, x.ProductClassId, x.PeriodYear, x.PeriodMonth });

        b.Property(x => x.TotalAQ_MWH).HasColumnType("decimal(18,2)");

        b.Property(x => x.CreatedAt)
         .HasDefaultValueSql("NOW() AT TIME ZONE 'UTC'");

        // Shipper → ShipperProductClass (1:N)
        b.HasOne(x => x.Shipper)
         .WithMany(s => s.ProductClasses)
         .HasForeignKey(x => x.ShipperId)
         .OnDelete(DeleteBehavior.Cascade)
         .HasConstraintName("FK_ShipperProductClass_Shipper");

        // ProductClass → ShipperProductClass (1:N)
        b.HasOne(x => x.ProductClass)
         .WithMany(pc => pc.ShipperProductClasses)
         .HasForeignKey(x => x.ProductClassId)
         .OnDelete(DeleteBehavior.Restrict)
         .HasConstraintName("FK_ShipperProductClass_ProductClass");

        b.HasIndex(x => new { x.ShipperId, x.PeriodYear, x.PeriodMonth })
         .HasDatabaseName("IX_ShipperProductClass_Shipper_Period");
    }
}
