namespace PAFA.Domain.Interfaces;

/// <summary>
/// Abstraction for sending transactional emails (welcome, password reset, etc.).
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends a welcome email to a newly created user with first-login instructions.
    /// </summary>
    Task SendWelcomeEmailAsync(
        string recipientEmail,
        string firstName,
        string temporaryPassword,
        CancellationToken ct = default);
}
