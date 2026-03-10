using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PAFA.Domain.Entities;

namespace PAFA.Infrastructure.EntityConfigurations ; 

public class ShipperConfiguration : IEntityTypeConfiguration<Shipper>
{
    public void Configure(EntityTypeBuilder<Shipper> b)
    {
        b.ToTable("Shipper", "dbo");

        b.HasKey(x => x.Id);
        b.Property(x => x.Id)
         .HasDefaultValueSql("gen_random_uuid()");

        b.Property(x => x.Name)
         .IsRequired()
         .HasMaxLength(200);

        b.Property(x => x.ShortCode)
         .IsRequired()
         .HasMaxLength(10);

        b.HasIndex(x => x.ShortCode)
         .IsUnique()
         .HasDatabaseName("UK_Shipper_ShortCode");

        b.Property(x => x.LegalEntity).HasMaxLength(300);
        b.Property(x => x.ContactEmail).HasMaxLength(254);
        b.Property(x => x.ContactName).HasMaxLength(200);

        b.Property(x => x.IsActive).HasDefaultValue(true);

        b.Property(x => x.CreatedAt)
         .HasDefaultValueSql("NOW() AT TIME ZONE 'UTC'");

        // Index
        b.HasIndex(x => new { x.IsActive, x.Name })
         .HasDatabaseName("IX_Shipper_IsActive_Name");

        b.HasIndex(x => x.Name)
         .HasDatabaseName("IX_Shipper_Name");
    }
}