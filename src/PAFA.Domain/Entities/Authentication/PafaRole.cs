using PAFA.Domain.Entities.Authentication;

/// <summary>
/// Reference table — 4 fixed roles seeded at startup.
///
/// Id | Name       | Who
/// ---+------------+------------------------------------------------
///  1 | PafaUser   | Gemserv analyst — read reports, export, dashboard
///  2 | PafaAdmin  | Gemserv admin — full access + user management
///  3 | PacMember  | PAC member — read B-reports (non-anonymised)
///  4 | Shipper    | Shipper contact — read own data only (A-reports)
/// </summary>
public class PafaRole
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    // ── Navigation ─────────────────────────────────────────────
    public ICollection<PafaUserRole> UserRoles { get; set; } = new List<PafaUserRole>();
    public ICollection<PafaRolePermission> RolePermissions { get; set; } = new List<PafaRolePermission>();
}
