using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PAFA.Domain.Entities.Authentication;

namespace PAFA.Infrastructure.EntityConfigurations;

public class PafaRolePermissionConfiguration : IEntityTypeConfiguration<PafaRolePermission>
{
    public void Configure(EntityTypeBuilder<PafaRolePermission> b)
    {
        b.ToTable("pafa_role_permissions");

        b.HasKey(rp => new { rp.RoleId, rp.PermissionId });

        b.HasOne(rp => rp.Role)
         .WithMany()
         .HasForeignKey(rp => rp.RoleId);

        b.HasOne(rp => rp.Permission)
         .WithMany(p => p.RolePermissions)
         .HasForeignKey(rp => rp.PermissionId);

        // ── Seed — permission matrix ────────────────────────────────────
        // Role 1 = PafaUser   : view anon + download
        // Role 2 = PafaAdmin  : ALL permissions
        // Role 3 = PacMember  : view anon + view non-anon + download
        // Role 4 = Shipper    : view anon + download

        b.HasData(
            // ── PafaUser (1) ─────────────────────────────────────────────
            new PafaRolePermission { RoleId = 1, PermissionId = 3 }, // reports.anonymised.view
            new PafaRolePermission { RoleId = 1, PermissionId = 7 }, // reports.download

            // ── PafaAdmin (2) — full privileges ──────────────────────────
            new PafaRolePermission { RoleId = 2, PermissionId = 1 }, // users.create
            new PafaRolePermission { RoleId = 2, PermissionId = 2 }, // users.delete
            new PafaRolePermission { RoleId = 2, PermissionId = 3 }, // reports.anonymised.view
            new PafaRolePermission { RoleId = 2, PermissionId = 4 }, // reports.nonanonymised.view
            new PafaRolePermission { RoleId = 2, PermissionId = 5 }, // reports.anonymised.edit
            new PafaRolePermission { RoleId = 2, PermissionId = 6 }, // reports.nonanonymised.edit
            new PafaRolePermission { RoleId = 2, PermissionId = 7 }, // reports.download

            // ── PacMember (3) ────────────────────────────────────────────
            new PafaRolePermission { RoleId = 3, PermissionId = 3 }, // reports.anonymised.view
            new PafaRolePermission { RoleId = 3, PermissionId = 4 }, // reports.nonanonymised.view
            new PafaRolePermission { RoleId = 3, PermissionId = 7 }, // reports.download

            // ── Shipper (4) ──────────────────────────────────────────────
            new PafaRolePermission { RoleId = 4, PermissionId = 3 }, // reports.anonymised.view
            new PafaRolePermission { RoleId = 4, PermissionId = 7 }  // reports.download
        );
    }
}
