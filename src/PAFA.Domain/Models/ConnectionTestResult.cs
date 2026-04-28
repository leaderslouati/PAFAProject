namespace PAFA.Domain.Models;

/// <summary>
/// Result of a remote file source connectivity test.
/// Distinguishes authentication failures from network/other errors.
/// </summary>
public sealed record ConnectionTestResult(
    bool IsConnected,
    ConnectionErrorType ErrorType = ConnectionErrorType.None,
    string? ErrorMessage = null)
{
    public static ConnectionTestResult Success() => new(true);

    public static ConnectionTestResult AuthenticationFailure(string message)
        => new(false, ConnectionErrorType.Authentication, message);

    public static ConnectionTestResult Forbidden(string message)
        => new(false, ConnectionErrorType.Forbidden, message);

    public static ConnectionTestResult NetworkError(string message)
        => new(false, ConnectionErrorType.Network, message);

    public static ConnectionTestResult Unknown(string message)
        => new(false, ConnectionErrorType.Unknown, message);
}

public enum ConnectionErrorType
{
    None,
    Authentication,
    Forbidden,
    Network,
    Unknown
}
