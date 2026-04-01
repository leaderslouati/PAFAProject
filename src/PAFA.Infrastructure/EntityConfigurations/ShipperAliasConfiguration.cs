using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Graph.Models;
using PAFA.Domain.Entities.Referential;

namespace PAFA.Infrastructure.EntityConfigurations;

public class ShipperAliasConfiguration : IEntityTypeConfiguration<ShipperAlias>
{
    public void Configure(EntityTypeBuilder<ShipperAlias> builder)
    {
        builder.ToTable("shipperAlias"); 

        builder.HasKey(x => x.Id);

        builder.Property(e => e.AliasCode)
                     .IsRequired()
                     .HasMaxLength(50);

        builder.Property(e => e.ValidFrom)
              .IsRequired();

        // ValidTo nullable = alias actif
        builder.Property(e => e.ValidTo)
              .IsRequired(false);

        builder.Property(e => e.IsActive)
              .HasDefaultValue(true);

        // Index : accès rapide aux alias actifs par shipper
        builder.HasIndex(e => new { e.ShipperId, e.IsActive })
              .HasDatabaseName("IX_shipperAliases_ShipperId_IsActive");

        // Relation : un shipper peut avoir plusieurs alias
        builder.HasOne(e => e.Shipper)
              .WithMany(s => s.ShipperAliases)
              .HasForeignKey(e => e.ShipperId)
              .OnDelete(DeleteBehavior.Cascade);
    }
}