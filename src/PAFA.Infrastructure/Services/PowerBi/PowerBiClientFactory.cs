using Microsoft.Identity.Client;
using Microsoft.PowerBI.Api;
using Microsoft.Rest;

namespace PAFA.Infrastructure.Services.PowerBi;

/// <summary>
/// Creates authenticated <see cref="PowerBIClient"/> instances
/// using MSAL Service Principal (Client Credentials) flow.
/// Registered as Singleton — MSAL handles token caching internally.
/// </summary>
public sealed class PowerBiClientFactory
{
    private readonly PowerBiSettings _settings;
    private readonly IConfidentialClientApplication _msal;

    public PowerBiClientFactory(PowerBiSettings settings)
    {
        _settings = settings;
        _msal = ConfidentialClientApplicationBuilder
            .Create(_settings.ClientId)
            .WithClientSecret(_settings.ClientSecret)
            .WithAuthority(_settings.Authority)
            .Build();
    }

    /// <summary>
    /// Acquires a token and returns a ready-to-use <see cref="PowerBIClient"/>.
    /// Token caching is handled by MSAL — repeated calls within the token
    /// lifetime return a cached token without a network round-trip.
    /// </summary>
    public async Task<PowerBIClient> CreateAsync(CancellationToken ct = default)
    {
        var authResult = await _msal
            .AcquireTokenForClient(_settings.Scopes)
            .ExecuteAsync(ct);

        var tokenCredentials = new TokenCredentials(authResult.AccessToken, "Bearer");
        return new PowerBIClient(new Uri("https://api.powerbi.com"), tokenCredentials);
    }
}
