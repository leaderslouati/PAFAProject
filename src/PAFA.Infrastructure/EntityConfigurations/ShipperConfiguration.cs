using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PAFA.Domain.Entities.Referential;

namespace PAFA.Infrastructure.EntityConfigurations;

public class ShipperConfiguration : IEntityTypeConfiguration<Shipper>
{
    public void Configure(EntityTypeBuilder<Shipper> builder)
    {
        builder.ToTable("shippers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

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

        builder.Property(x => x.Email)
               .HasColumnName("email")
               .HasMaxLength(255);

        builder.Property(x => x.IsActive)
               .HasColumnName("is_active")
               .HasDefaultValue(true);

        builder.Property(x => x.MarketEntryDate)
               .HasColumnName("market_entry_date")
               .HasColumnType("date");

        builder.Property(x => x.MarketExitDate)
               .HasColumnName("market_exit_date")
               .HasColumnType("date");

        builder.Property(x => x.PortfolioSize)
               .HasColumnName("portfolio_size");

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

        builder.HasIndex(x => x.ShortCode)
               .IsUnique()
               .HasDatabaseName("ix_shipper_short_code");

        builder.HasIndex(x => x.IsActive)
               .HasDatabaseName("ix_shipper_is_active");

        // Navigation - Many-to-Many via ShipperProductClass
        builder.HasMany(x => x.ProductClasses)
               .WithOne(x => x.Shipper)
               .HasForeignKey(x => x.ShipperId)
               .OnDelete(DeleteBehavior.Cascade);

        // Navigation - One-to-Many MetricValues
        builder.HasMany(x => x.MetricValues)
               .WithOne(x => x.Shipper)
               .HasForeignKey(x => x.ShipperId)
               .OnDelete(DeleteBehavior.SetNull)
               .IsRequired(false);

        // Navigation - One-to-Many ShipperAliases
        builder.HasMany(x => x.ShipperAliases)
               .WithOne(x => x.Shipper)
               .HasForeignKey(x => x.ShipperId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}