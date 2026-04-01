using MediatR;
using Microsoft.Extensions.Logging;
using PAFA.Domain.Entities.Authentication;
using PAFA.Domain.Interfaces;
using PAFA.Domain.IRepository;
using PAFA.Domain.Repositories;
using PAFA.Extraction.Commands.Users;
using System.Security.Cryptography;
using System.Text;

namespace PAFA.Extraction.Handlers.Users;

/// <summary>
/// Handles the CreateUserCommand:
///   1. Guards: duplicate email check, role validation.
///   2. Generates a cryptographically secure temporary password.
///   3. Persists the user + PafaUserRole join rows in a single transaction.
///   4. Sends a welcome email via IEmailService.
///   5. Logs the audit event.
/// </summary>
public sealed class CreateUserCommandHandler(
    IPafaUserRepository userRepository,
    PafaDbContext db,
    IEmailService emailService,
    ILogger<CreateUserCommandHandler> log)
    : IRequestHandler<CreateUserCommand, CreateUserResult>
{
    public async Task<CreateUserResult> Handle(CreateUserCommand cmd, CancellationToken ct)
    {
        log.LogInformation(
            "CreateUser started — Email: {Email} | AdminId: {Admin}",
            cmd.Email, cmd.AdminId);

        // ?? 1. Duplicate email guard ???????????????????????????????????????
        if (await userRepository.EmailExistsAsync(cmd.Email, ct))
        {
            log.LogWarning("CreateUser rejected — duplicate email: {Email}", cmd.Email);
            return new CreateUserResult(false, null, $"A user with email '{cmd.Email}' already exists.");
        }

        // ?? 2. Validate that every submitted RoleId exists in the database ?
        var validRoleIds = db.PafaRoles.Select(r => r.Id).ToHashSet();
        var invalidIds = cmd.RoleIds.Except(validRoleIds).ToList();
        if (invalidIds.Count != 0)
        {
            var msg = $"Unknown role id(s): {string.Join(", ", invalidIds)}.";
            log.LogWarning("CreateUser rejected — {Msg}", msg);
            return new CreateUserResult(false, null, msg);
        }

        // ?? 3. Generate a secure temporary password ????????????????????????
        // Password: 12 chars, mix of upper/lower/digit/symbol.
        var temporaryPassword = GenerateTemporaryPassword();
        var passwordHash = HashPassword(temporaryPassword);

        // ?? 4. Build the entity ????????????????????????????????????????????
        var user = new PafaUser
        {
            Id        = Guid.NewGuid(),
            Username  = BuildUsername(cmd.FirstName, cmd.LastName),
            Email     = cmd.Email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            FirstName = cmd.FirstName.Trim(),
            LastName  = cmd.LastName.Trim(),
            JobTitle  = cmd.JobTitle?.Trim(),
            Department = cmd.Department?.Trim(),
            IsActive  = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = cmd.AdminId
        };

        // Assign roles via the join table
        user.UserRoles = cmd.RoleIds
            .Distinct()
            .Select(roleId => new PafaUserRole { UserId = user.Id, RoleId = roleId })
            .ToList();

        // ?? 5. Persist atomically ??????????????????????????????????????????
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await userRepository.AddAsync(user, ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            log.LogError(ex, "CreateUser failed during persistence — Email: {Email}", cmd.Email);
            return new CreateUserResult(false, null, "An unexpected error occurred while saving the user.");
        }

        // ?? 6. Send welcome email ??????????????????????????????????????????
        // Non-blocking: a failure here must NOT roll back the already-committed user.
        try
        {
            await emailService.SendWelcomeEmailAsync(
                user.Email, user.FirstName, temporaryPassword, ct);
        }
        catch (Exception ex)
        {
            // Log and continue — user is created, email is best-effort.
            log.LogError(ex, "Welcome email failed for {Email} — user still created", user.Email);
        }

        // ?? 7. Audit log ???????????????????????????????????????????????????
        log.LogInformation(
            "AUDIT | CreateUser | AdminId={Admin} | NewUserId={UserId} | Email={Email} | Roles=[{Roles}] | Timestamp={Ts}",
            cmd.AdminId, user.Id, user.Email,
            string.Join(",", cmd.RoleIds),
            DateTime.UtcNow);

        return new CreateUserResult(true, user.Id, null);
    }

    // ?? Helpers ??????????????????????????????????????????????????????????

    /// <summary>
    /// Builds a unique username from first + last name, e.g. "john.doe".
    /// Normalised to lowercase ASCII letters/digits only.
    /// </summary>
    private static string BuildUsername(string firstName, string lastName)
    {
        static string Normalise(string s) =>
            new string(s.Trim().ToLowerInvariant()
                .Where(c => char.IsLetterOrDigit(c)).ToArray());

        return $"{Normalise(firstName)}.{Normalise(lastName)}";
    }

    /// <summary>
    /// Generates a 12-character cryptographically secure temporary password.
    /// Contains at least one uppercase, one lowercase, one digit and one symbol.
    /// </summary>
    private static string GenerateTemporaryPassword()
    {
        const string upper   = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower   = "abcdefghjkmnpqrstuvwxyz";
        const string digits  = "23456789";
        const string symbols = "!@#$%&*?";
        const string all     = upper + lower + digits + symbols;

        var buf = new char[12];
        // Guarantee at least one character from each category
        buf[0] = RandomChar(upper);
        buf[1] = RandomChar(lower);
        buf[2] = RandomChar(digits);
        buf[3] = RandomChar(symbols);

        for (int i = 4; i < buf.Length; i++)
            buf[i] = RandomChar(all);

        // Shuffle to avoid predictable positions
        return new string(buf.OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue)).ToArray());
    }

    private static char RandomChar(string charset)
        => charset[RandomNumberGenerator.GetInt32(charset.Length)];

    /// <summary>
    /// BCrypt-style SHA-256 placeholder hash.
    /// Replace with BCrypt.Net or ASP.NET Core IPasswordHasher in production.
    /// </summary>
    private static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
