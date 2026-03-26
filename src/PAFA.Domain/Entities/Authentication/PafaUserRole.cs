namespace PAFA.Domain.Entities.Authentication; 

/// <summary>
/// Many-to-many join between PafaUser and PafaRole.
/// Composite PK: UserId + RoleId.
/// A user may hold multiple roles (e.g. PafaAdmin also has PafaUser).
/// </summary>
public class PafaUserRole
{
    public Guid UserId { get; set; }
    public int RoleId { get; set; }

    // ── Navigation ─────────────────────────────────────────────
    public PafaUser User { get; set; } = null!;
    public PafaRole Role { get; set; } = null!;
}
