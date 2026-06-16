using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PAFA.Domain.Entities.Referential;

namespace PAFA.Infrastructure.EntityConfigurations;

public class ShipperAliasConfiguration : IEntityTypeConfiguration<ShipperAlias>
{
    public void Configure(EntityTypeBuilder<ShipperAlias> builder)
    {
        builder.ToTable("shipper_alias");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
               .HasColumnName("id")
               .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.ShipperId)
               .HasColumnName("shipper_id")
               .IsRequired();

        builder.Property(x => x.AliasCode)
               .HasColumnName("alias_code")
               .HasMaxLength(50)
               .IsRequired();

        builder.Property(x => x.ValidFrom)
               .HasColumnName("valid_from")
               .HasColumnType("timestamp with time zone");

        builder.Property(x => x.ValidTo)
               .HasColumnName("valid_to")
               .HasColumnType("timestamp with time zone")
               .IsRequired(false);

        builder.Property(x => x.IsActive)
               .HasColumnName("is_active")
               .HasDefaultValue(true);

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
               .HasDatabaseName("ix_shipper_alias_shipper_id");

        builder.HasIndex(x => x.AliasCode)
               .HasDatabaseName("ix_shipper_alias_code");

        // FK Relationship
        builder.HasOne(x => x.Shipper)
               .WithMany(x => x.ShipperAliases)
               .HasForeignKey(x => x.ShipperId)
               .IsRequired()
               .OnDelete(DeleteBehavior.Cascade);
    }
}