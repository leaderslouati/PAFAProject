using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PAFA.Domain.Entities.Authentication;
namespace PAFA.Infrastructure.EntityConfigurations;

public class PafaUserConfiguration : IEntityTypeConfiguration<PafaUser>
{
    public void Configure(EntityTypeBuilder<PafaUser> b)
    {
        b.ToTable("pafa_users");

        b.HasKey(u => u.Id);

        b.Property(u => u.Username)
         .IsRequired()
         .HasMaxLength(100);

        b.HasIndex(u => u.Username).IsUnique();

        b.Property(u => u.Email)
         .IsRequired()
         .HasMaxLength(200);

        b.HasIndex(u => u.Email).IsUnique();

        b.Property(u => u.PasswordHash).IsRequired().HasMaxLength(500);
        b.Property(u => u.FirstName).HasMaxLength(100);
        b.Property(u => u.LastName).HasMaxLength(100);
        b.Property(u => u.JobTitle).HasMaxLength(150);
        b.Property(u => u.Department).HasMaxLength(150);
        b.Property(u => u.RowVersion).IsRowVersion();
        b.HasQueryFilter(u => !u.IsDeleted);
       
    }
}

