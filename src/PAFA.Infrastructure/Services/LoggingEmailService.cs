using Microsoft.Extensions.Logging;
using PAFA.Domain.Interfaces;

namespace PAFA.Infrastructure.Services;

/// <summary>
/// POC implementation — logs the email content instead of sending it.
/// Replace with a real SMTP/SendGrid/Graph implementation in production.
/// </summary>
public class LoggingEmailService(ILogger<LoggingEmailService> log) : IEmailService
{
    public Task SendWelcomeEmailAsync(
        string recipientEmail, string firstName, string temporaryPassword,
        CancellationToken ct = default)
    {
        log.LogInformation(
            "?? Welcome email ? {Email} | Name: {Name} | TempPassword: {Pass}",
            recipientEmail, firstName, temporaryPassword);

        return Task.CompletedTask;
    }
}
