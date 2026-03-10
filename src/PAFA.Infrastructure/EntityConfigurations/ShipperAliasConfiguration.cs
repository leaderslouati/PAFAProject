using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PAFA.Domain.Entities;

namespace PAFA.Infrastructure.EntityConfigurations ; 
// ════════════════════════════════════════════════════════════════════════
//  SHIPPER ALIAS
// ════════════════════════════════════════════════════════════════════════
public class ShipperAliasConfiguration : IEntityTypeConfiguration<ShipperAlias>
{
    public void Configure(EntityTypeBuilder<ShipperAlias> b)
    {
        b.ToTable("ShipperAlias", "dbo");

        b.HasKey(x => x.Id);

        b.Property(x => x.AliasCode)
         .IsRequired()
         .HasMaxLength(20);

        b.HasIndex(x => x.AliasCode)
         .IsUnique()
         .HasDatabaseName("UK_ShipperAlias_Code");

        b.Property(x => x.CreatedAt)
         .HasDefaultValueSql("NOW() AT TIME ZONE 'UTC'");
        b.Property(x => x.CreatedBy).HasMaxLength(100).HasDefaultValue("SYSTEM");

        // Relation: ShipperAlias → Shipper (N:1)
        b.HasOne(x => x.Shipper)
         .WithMany(s => s.Aliases)
         .HasForeignKey(x => x.ShipperId)
         .OnDelete(DeleteBehavior.Restrict)  // Shipper deletion not allowed if alias is active
         .HasConstraintName("FK_ShipperAlias_Shipper");

        b.HasIndex(x => new { x.ShipperId, x.ValidFrom, x.ValidTo })
         .HasDatabaseName("IX_ShipperAlias_ShipperId_Period");
    }
}