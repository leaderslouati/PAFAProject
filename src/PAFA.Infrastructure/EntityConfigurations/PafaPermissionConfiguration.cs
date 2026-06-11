using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PAFA.Domain.Entities.Authentication;

namespace PAFA.Infrastructure.EntityConfigurations;

public class PafaPermissionConfiguration : IEntityTypeConfiguration<PafaPermission>
{
    public void Configure(EntityTypeBuilder<PafaPermission> b)
    {
        b.ToTable("pafa_permissions");

        b.HasKey(p => p.Id);

        b.Property(p => p.Code)
         .IsRequired()
         .HasMaxLength(100);

        b.HasIndex(p => p.Code).IsUnique();

        b.Property(p => p.Description).HasMaxLength(300);

        // ── Seed ────────────────────────────────────────────────────────
        b.HasData(
            new PafaPermission { Id = 1, Code = "users.create",                Description = "Create user accounts" },
            new PafaPermission { Id = 2, Code = "users.delete",                Description = "Delete user accounts" },
            new PafaPermission { Id = 3, Code = "reports.anonymised.view",     Description = "View Schedule 2A (anonymised) reports" },
            new PafaPermission { Id = 4, Code = "reports.nonanonymised.view",  Description = "View Schedule 2B (non-anonymised) reports" },
            new PafaPermission { Id = 5, Code = "reports.anonymised.edit",     Description = "Edit Schedule 2A reports (observations)" },
            new PafaPermission { Id = 6, Code = "reports.nonanonymised.edit",  Description = "Edit Schedule 2B reports (observations)" },
            new PafaPermission { Id = 7, Code = "reports.download",            Description = "Download report files (PDF/Excel/PPTX)" }
        );
    }
}
