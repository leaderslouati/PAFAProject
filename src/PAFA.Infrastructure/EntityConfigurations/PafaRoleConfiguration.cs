using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PAFA.Infrastructure.EntityConfigurations;
public class PafaRoleConfiguration : IEntityTypeConfiguration<PafaRole>
{
    public void Configure(EntityTypeBuilder<PafaRole> b)
    {
        b.ToTable("pafa_roles");

        b.HasKey(r => r.Id);

        b.Property(r => r.Name)
         .IsRequired()
         .HasMaxLength(50);

        b.HasIndex(r => r.Name).IsUnique();

        b.Property(r => r.Role)
        .IsRequired()
        .HasMaxLength(50);

        b.HasIndex(r => r.Role).IsUnique();
        b.Property(r => r.Description).HasMaxLength(300);

        b.HasData(
           new PafaRole { Id = 1, Role = "PAFA_USER", Name = "PafaUser", Description = "Gemserv analyst — read reports" },
           new PafaRole { Id = 2, Role = "PAFA_ADMIN", Name = "PafaAdmin", Description = "Admin full access" },
           new PafaRole { Id = 3, Role = "PAC_MEMBER", Name = "PacMember", Description = "PAC access" },
           new PafaRole { Id = 4, Role = "SHIPPER", Name = "Shipper", Description = "Own data access" }
       );
    }
}
