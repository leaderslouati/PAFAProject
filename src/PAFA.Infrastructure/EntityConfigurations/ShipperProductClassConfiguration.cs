using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PAFA.Domain.Entities.Referential;

namespace PAFA.Infrastructure.EntityConfigurations;

public class ShipperProductClassConfiguration : IEntityTypeConfiguration<ShipperProductClass>
{
    public void Configure(EntityTypeBuilder<ShipperProductClass> builder)
    {
        builder.ToTable("shipper_product_classes");
  
        builder.Property(x => x.ShipperId)
               .HasColumnName("shipper_id")
               .IsRequired();

        builder.Property(x => x.ProductClassId)
               .HasColumnName("product_class_id")
               .IsRequired();


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

        builder.HasIndex(x => x.ShipperId)
               .HasDatabaseName("ix_spc_shipper_id");

        builder.HasIndex(x => x.ProductClassId)
               .HasDatabaseName("ix_spc_product_class_id");

        builder.HasIndex(x => new { x.ShipperId, x.ProductClassId })
               .IsUnique()
               .HasDatabaseName("ux_spc_shipper_product_class");

        // FK Relationships
        builder.HasOne(x => x.Shipper)
               .WithMany(x => x.ProductClasses)
               .HasForeignKey(x => x.ShipperId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ProductClass)
               .WithMany(x => x.ShipperProductClasses)
               .HasForeignKey(x => x.ProductClassId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
