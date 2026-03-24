using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PAFA.Domain.Entities.Referential;
using System;

namespace PAFA.Infrastructure.EntityConfigurations ; 
    public class ProductClassConfiguration : IEntityTypeConfiguration<ProductClass>
    {
        public void Configure(EntityTypeBuilder<ProductClass> b)
        {
            b.ToTable("product_classes");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Code).IsRequired().HasMaxLength(10);
            b.HasIndex(x => x.Code).IsUnique().HasDatabaseName("ix_pc_code");
            b.Property(x => x.AQThresholdLow).HasColumnType("numeric(12,4)");
            b.Property(x => x.AQThresholdHigh).HasColumnType("numeric(12,4)");
            b.Property(x => x.MinReadPercentage).HasColumnType("numeric(6,3)");
            b.Property(x => x.CreatedBy).HasMaxLength(100);
            b.Property(x => x.UpdatedBy).HasMaxLength(100);
            b.Property(x => x.RowVersion).IsConcurrencyToken().IsRequired(false);

            b.HasData(
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
                    Id = 2, Code = "PC2", Description = "Medium NDM", IsActive = true,
                    CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    CreatedBy = "SYSTEM"
                },
                new ProductClass
                {
                    Id = 3, Code = "PC3", Description = "Small NDM WAR", IsActive = true,
                    CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    CreatedBy = "SYSTEM"
                },
                new ProductClass
                {
                    Id = 4, Code = "PC4", Description = "IGT Small", IsActive = true,
                    CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    CreatedBy = "SYSTEM"
                });
        }
    }
