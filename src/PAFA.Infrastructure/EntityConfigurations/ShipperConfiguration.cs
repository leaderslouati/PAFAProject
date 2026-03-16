using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PAFA.Domain.Entities;

namespace PAFA.Infrastructure.EntityConfigurations;  

public class ShipperConfiguration : IEntityTypeConfiguration<Shipper>
{
    public void Configure(EntityTypeBuilder<Shipper> b)
    {
        b.ToTable("shippers");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.Name).IsRequired().HasMaxLength(200);
        b.Property(x => x.ShortCode).IsRequired().HasMaxLength(3).IsFixedLength();
        b.HasIndex(x => x.ShortCode).IsUnique().HasDatabaseName("ix_shipper_ssc");
        b.Property(x => x.LegalEntity).HasMaxLength(200);
        b.Property(x => x.Email).HasMaxLength(255);
        b.Property(x => x.MarketEntryDate).HasColumnType("date");
        b.Property(x => x.MarketExitDate).HasColumnType("date");
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);
        b.Property(x => x.RowVersion)
            .IsRowVersion()
            .IsRequired(false)
            .HasDefaultValueSql("decode('', 'hex')");
    }
}