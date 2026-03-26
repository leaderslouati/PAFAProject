using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PAFA.Domain.Entities.Authentication;

namespace PAFA.Infrastructure.EntityConfigurations;
public class PafaUserRoleConfiguration : IEntityTypeConfiguration<PafaUserRole>
{
    public void Configure(EntityTypeBuilder<PafaUserRole> b)
    {
        b.ToTable("pafa_user_roles");

        // Composite PK
        b.HasKey(ur => new { ur.UserId, ur.RoleId });

        b.HasOne(ur => ur.User)
         .WithMany(u => u.UserRoles)
         .HasForeignKey(ur => ur.UserId)
         .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(ur => ur.Role)
         .WithMany(r => r.UserRoles)
         .HasForeignKey(ur => ur.RoleId)
         .OnDelete(DeleteBehavior.Restrict);
    }
}
