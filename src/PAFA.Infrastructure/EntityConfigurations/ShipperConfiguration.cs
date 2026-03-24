using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PAFA.Domain.Entities.Referential;

public class ShipperConfiguration : IEntityTypeConfiguration<Shipper>
{
    public void Configure(EntityTypeBuilder<Shipper> builder)
    {
        builder.ToTable("shippers");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ShortCode)
               .HasColumnName("short_code")
               .HasMaxLength(20)
               .IsRequired();

        builder.Property(x => x.Name)
               .HasColumnName("name")
               .HasMaxLength(150)
               .IsRequired();

        builder.Property(x => x.LegalEntity)
               .HasColumnName("legal_entity")
               .HasMaxLength(150);

        builder.Property(x => x.IsActive)
               .HasColumnName("is_active")
               .HasDefaultValue(true);

        builder.HasIndex(x => x.ShortCode)
               .IsUnique()
               .HasDatabaseName("ix_shipper_short_code");

    }
}