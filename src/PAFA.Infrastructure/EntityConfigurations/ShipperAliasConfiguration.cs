using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PAFA.Domain.Entities.Referential;

namespace PAFA.Infrastructure.EntityConfigurations;

public class ShipperAliasConfiguration : IEntityTypeConfiguration<ShipperAlias>
{
    public void Configure(EntityTypeBuilder<ShipperAlias> builder)
    {
        builder.ToTable("shipperAlias"); 

        builder.HasKey(x => x.Id);

        builder.Property(x => x.AliasCode)
               .HasColumnName("alias_code")
               .HasMaxLength(20)
               .IsRequired();

      
        builder.HasOne(x => x.Shipper) 
               .WithMany()            
               .HasForeignKey(x => x.ShipperId) 
               .OnDelete(DeleteBehavior.Restrict);
    }
}