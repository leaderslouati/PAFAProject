// ═══════════════════════════════════════════════════════════
// PAFA.Infrastructure/Services/PowerBi/PowerBiClientFactory.cs
// PURPOSE: Acquires an AAD token via MSAL (client credentials)
//          and returns an authenticated PowerBIClient instance.
// ═══════════════════════════════════════════════════════════
using Microsoft.Identity.Client;
using Microsoft.PowerBI.Api;
using Microsoft.Rest;

namespace PAFA.Infrastructure.Services.PowerBi;

public sealed class PowerBiClientFactory(PowerBiSettings settings)
{
    // Power BI REST API scope — fixed value, not configurable.
    private static readonly string[] Scopes =
        ["https://analysis.windows.net/powerbi/api/.default"];

    /// <summary>
    /// Acquires an AAD access token using the configured service principal
    /// and returns a ready-to-use <see cref="PowerBIClient"/>.
    /// </summary>
    public async Task<PowerBIClient> CreateAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(settings.TenantId)
            || string.IsNullOrWhiteSpace(settings.ClientId)
            || string.IsNullOrWhiteSpace(settings.ClientSecret))
        {
            throw new InvalidOperationException(
                "Power BI service principal credentials are not configured. " +
                "Set PowerBi:TenantId, PowerBi:ClientId and PowerBi:ClientSecret.");
        }

        var app = ConfidentialClientApplicationBuilder
            .Create(settings.ClientId)
            .WithClientSecret(settings.ClientSecret)
            .WithTenantId(settings.TenantId)
            .Build();

        var authResult = await app
            .AcquireTokenForClient(Scopes)
            .ExecuteAsync(ct);

        var tokenCredentials = new TokenCredentials(authResult.AccessToken, "Bearer");
        return new PowerBIClient(new Uri("https://api.powerbi.com/"), tokenCredentials);
    }
}
