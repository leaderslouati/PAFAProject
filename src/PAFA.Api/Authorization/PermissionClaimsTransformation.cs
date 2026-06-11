using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using PAFA.Infrastructure.Persistence;

namespace PAFA.Api.Authorization;

/// <summary>
/// Enriches the authenticated ClaimsPrincipal with permission claims
/// loaded from the database (pafa_role_permissions + pafa_permissions).
/// Runs once per request after JWT validation.
/// </summary>
public sealed class PermissionClaimsTransformation(
    IServiceScopeFactory scopeFactory) : IClaimsTransformation
{
    public const string PermissionClaimType = "permission";

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var identity = principal.Identity as ClaimsIdentity;
        if (identity is null || !identity.IsAuthenticated)
            return principal;

        // Avoid re-processing if already enriched
        if (identity.HasClaim(c => c.Type == PermissionClaimType))
            return principal;

        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? principal.FindFirstValue("sub");

        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var uid))
            return principal;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PafaDbContext>();

        var permissions = await db.PafaUserRoles
            .Where(ur => ur.UserId == uid)
            .Join(db.PafaRolePermissions,
                  ur => ur.RoleId,
                  rp => rp.RoleId,
                  (ur, rp) => rp.PermissionId)
            .Distinct()
            .Join(db.PafaPermissions,
                  pid => pid,
                  p => p.Id,
                  (_, p) => p.Code)
            .ToListAsync();

        foreach (var code in permissions)
            identity.AddClaim(new Claim(PermissionClaimType, code));

        // Also add role names for [Authorize(Roles = "...")] backwards compatibility
        var roles = await db.PafaUserRoles
            .Where(ur => ur.UserId == uid)
            .Join(db.PafaRoles,
                  ur => ur.RoleId,
                  r => r.Id,
                  (_, r) => r.Name)
            .ToListAsync();

        foreach (var role in roles)
        {
            if (!identity.HasClaim(ClaimTypes.Role, role))
                identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }

        return principal;
    }
}
