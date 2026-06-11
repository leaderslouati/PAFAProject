namespace PAFA.Domain.Entities.Authentication;

/// <summary>
/// Granular permission that can be assigned to roles.
/// Seed data covers: users.create, users.delete,
/// reports.anonymised.view, reports.nonanonymised.view,
/// reports.anonymised.edit, reports.nonanonymised.edit,
/// reports.download.
/// </summary>
public class PafaPermission
{
    public int Id { get; set; }

    /// <summary>Dot-notation code, e.g. "reports.anonymised.view".</summary>
    public string Code { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    // ── Navigation ─────────────────────────────────────────────
    public ICollection<PafaRolePermission> RolePermissions { get; set; }
        = new List<PafaRolePermission>();
}
