using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PAFA.Domain.Entities;

namespace PAFA.Infrastructure.EntityConfigurations ; 
public class ProductClassConfiguration : IEntityTypeConfiguration<ProductClass>
{
    public void Configure(EntityTypeBuilder<ProductClass> b)
    {
        b.ToTable("ProductClass", "dbo");

        b.HasKey(x => x.Id);

        b.Property(x => x.Code)
         .IsRequired()
         .HasMaxLength(5);

        b.HasIndex(x => x.Code)
         .IsUnique()
         .HasDatabaseName("UK_ProductClass_Code");

        b.Property(x => x.Description).IsRequired().HasMaxLength(500);

        b.Property(x => x.AQThresholdLow).HasColumnType("decimal(18,2)");
        b.Property(x => x.AQThresholdHigh).HasColumnType("decimal(18,2)");
        b.Property(x => x.MinReadPercentage).HasColumnType("decimal(5,2)");

        b.Property(x => x.IsActive).HasDefaultValue(true);

        // Seed data
        b.HasData(
            new ProductClass { Id=1, Code="PC1", Description="Class 1 – AQ > 732 MWH (Industrial, large sites)",    MinReadPercentage=97.5m },
            new ProductClass { Id=2, Code="PC2", Description="Class 2 – Quarterly read frequency",                  AQThresholdLow=73.2m, AQThresholdHigh=732m },
            new ProductClass { Id=3, Code="PC3", Description="Class 3 – Annual read frequency",                     AQThresholdLow=0m,    AQThresholdHigh=73.2m },
            new ProductClass { Id=4, Code="PC4", Description="Class 4 – Low read frequency / automated metering",   AQThresholdLow=0m }
        );
    }
}