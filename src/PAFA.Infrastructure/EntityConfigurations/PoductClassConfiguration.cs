using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PAFA.Domain.Entities.Referential;

namespace PAFA.Infrastructure.EntityConfigurations;

public class ProductClassConfiguration : IEntityTypeConfiguration<ProductClass>
{
    public void Configure(EntityTypeBuilder<ProductClass> builder)
    {
        builder.ToTable("product_classes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
               .ValueGeneratedNever()
               .HasColumnName("id");

        builder.Property(x => x.Code)
               .HasColumnName("code")
               .HasMaxLength(10)
               .IsRequired();

        builder.Property(x => x.Description)
               .HasColumnName("description")
               .HasMaxLength(2000)
               .IsRequired();

        builder.Property(x => x.AQThresholdLow)
               .HasColumnName("aq_threshold_low")
               .HasColumnType("numeric(12,4)");

        builder.Property(x => x.AQThresholdHigh)
               .HasColumnName("aq_threshold_high")
               .HasColumnType("numeric(12,4)");

        builder.Property(x => x.MinReadPercentage)
               .HasColumnName("min_read_percentage")
               .HasColumnType("numeric(6,3)");

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

        builder.HasIndex(x => x.Code)
               .IsUnique()
               .HasDatabaseName("ix_pc_code");

        // Relationships
        builder.HasMany(x => x.ShipperProductClasses)
               .WithOne(x => x.ProductClass)
               .HasForeignKey(x => x.ProductClassId)
               .OnDelete(DeleteBehavior.Cascade);

        // Seed Data
        builder.HasData(
            new ProductClass
            {
                Id = 1,
                Code = "PC1",
                Description = "Large sites — AQ ≥ 732 MWH",
                AQThresholdLow = 732m,
                MinReadPercentage = 97.5m,
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedBy = "SYSTEM"
            },
            new ProductClass
            {
                Id = 2,
                Code = "PC2",
                Description = "Medium NDM",
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedBy = "SYSTEM"
            },
            new ProductClass
            {
                Id = 3,
                Code = "PC3",
                Description = "Small NDM WAR",
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedBy = "SYSTEM"
            },
            new ProductClass
            {
                Id = 4,
                Code = "PC4",
                Description = "IGT Small",
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedBy = "SYSTEM"
            });
    }
}
