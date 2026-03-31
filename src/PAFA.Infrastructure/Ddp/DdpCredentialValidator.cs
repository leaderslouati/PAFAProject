using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PAFA.Domain.Interfaces;
using System.Net.Http.Headers;

namespace PAFA.Infrastructure.Ddp;

public class DdpCredentialValidator : IDdpCredentialValidator
{
    private readonly DdpSettings _settings;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<DdpCredentialValidator> _log;

    public DdpCredentialValidator(
        IOptions<DdpSettings> settings,
        IHttpClientFactory httpFactory,
        ILogger<DdpCredentialValidator> log)
    {
        _settings = settings.Value;
        _httpFactory = httpFactory;
        _log = log;
    }

    public async Task<(bool IsValid, string? Message)> ValidateAsync(string? username, string? token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return (false, "DDP token is missing or empty.");

        if (string.IsNullOrWhiteSpace(username))
            return (false, "DDP username is missing or empty.");

        if (!_settings.UseHttpValidation || string.IsNullOrWhiteSpace(_settings.ApiBaseUrl))
        {
            // Basic validation only: token length heuristic
            if (token.Length < 8)
                return (false, "DDP token appears invalid (too short).");
            return (true, null);
        }

        try
        {
            var client = _httpFactory.CreateClient("ddp-client");
            client.BaseAddress = new Uri(_settings.ApiBaseUrl);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var endpoint = _settings.ValidateEndpoint?.TrimStart('/') ?? "health";
            var res = await client.GetAsync(endpoint, ct);
            if (res.IsSuccessStatusCode)
                return (true, null);
            var body = await res.Content.ReadAsStringAsync(ct);
            _log.LogWarning("DDP credential validation failed: {Status} {Body}", res.StatusCode, body);
            return (false, $"DDP validation failed: {res.StatusCode}");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error while validating DDP credentials");
            return (false, "Unable to validate DDP credentials (network error).");
        }
    }
}
