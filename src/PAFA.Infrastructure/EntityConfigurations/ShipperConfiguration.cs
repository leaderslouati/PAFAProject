using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class ShipperConfiguration : IEntityTypeConfiguration<Shipper>
{
    // Static seed date — MUST be a constant, not DateTime.Now or DateTime.UtcNow
    private static readonly DateTime SeedDate = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

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

        // Seed data with STATIC Guids — never use Guid.NewGuid() in HasData
        builder.HasData(
            new Shipper
            {
                Id = Guid.Parse("a0000001-0000-0000-0000-000000000001"),
                ShortCode = "SHIP_A",
                Name = "Alpha Gas Ltd",
                LegalEntity = "Alpha Gas Limited",
                IsActive = true,
                CreatedAt = SeedDate,
                CreatedBy = "SEED"
            },
            new Shipper
            {
                Id = Guid.Parse("a0000001-0000-0000-0000-000000000002"),
                ShortCode = "SHIP_B",
                Name = "Beta Energy plc",
                LegalEntity = "Beta Energy PLC",
                IsActive = true,
                CreatedAt = SeedDate,
                CreatedBy = "SEED"
            },
            new Shipper
            {
                Id = Guid.Parse("a0000001-0000-0000-0000-000000000003"),
                ShortCode = "SHIP_C",
                Name = "Gamma Supply Ltd",
                LegalEntity = "Gamma Supply Limited",
                IsActive = true,
                CreatedAt = SeedDate,
                CreatedBy = "SEED"
            },
            new Shipper
            {
                Id = Guid.Parse("a0000001-0000-0000-0000-000000000004"),
                ShortCode = "SHIP_D",
                Name = "Delta Gas Co",
                LegalEntity = "Delta Gas Company",
                IsActive = true,
                CreatedAt = SeedDate,
                CreatedBy = "SEED"
            },
            new Shipper
            {
                Id = Guid.Parse("a0000001-0000-0000-0000-000000000005"),
                ShortCode = "SHIP_E",
                Name = "Epsilon Energy",
                LegalEntity = "Epsilon Energy Ltd",
                IsActive = true,
                CreatedAt = SeedDate,
                CreatedBy = "SEED"
            },
            new Shipper
            {
                Id = Guid.Parse("a0000001-0000-0000-0000-000000000006"),
                ShortCode = "SHIP_F",
                Name = "Zeta Gas Ltd",
                LegalEntity = "Zeta Gas Limited",
                IsActive = true,
                CreatedAt = SeedDate,
                CreatedBy = "SEED"
            },
            new Shipper
            {
                Id = Guid.Parse("a0000001-0000-0000-0000-000000000007"),
                ShortCode = "SHIP_G",
                Name = "Eta Supply plc",
                LegalEntity = "Eta Supply PLC",
                IsActive = true,
                CreatedAt = SeedDate,
                CreatedBy = "SEED"
            },
            new Shipper
            {
                Id = Guid.Parse("a0000001-0000-0000-0000-000000000008"),
                ShortCode = "SHIP_H",
                Name = "Theta Gas Corp",
                LegalEntity = "Theta Gas Corporation",
                IsActive = true,
                CreatedAt = SeedDate,
                CreatedBy = "SEED"
            }
        );
    }
}