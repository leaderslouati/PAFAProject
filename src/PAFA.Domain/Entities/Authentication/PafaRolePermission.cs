namespace PAFA.Domain.Entities.Authentication;

/// <summary>
/// Many-to-many join between PafaRole and PafaPermission.
/// Composite PK: RoleId + PermissionId.
/// </summary>
public class PafaRolePermission
{
    public int RoleId { get; set; }
    public int PermissionId { get; set; }

    // ── Navigation ─────────────────────────────────────────────
    public PafaRole Role { get; set; } = null!;
    public PafaPermission Permission { get; set; } = null!;
}
