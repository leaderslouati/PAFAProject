namespace PAFA.Domain.Interfaces;

public interface IDdpCredentialValidator
{
    /// <summary>
    /// Validate credentials for accessing DDP resources.
    /// Returns (isValid, message). Message contains error details when invalid.
    /// </summary>
    Task<(bool IsValid, string? Message)> ValidateAsync(string? username, string? token, CancellationToken ct = default);
}
