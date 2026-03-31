namespace PAFA.Infrastructure.Ddp;

public class DdpSettings
{
    public const string SectionName = "Ddp";

    /// <summary>Base URL for the DDP API (optional).</summary>
    public string ApiBaseUrl { get; set; } = string.Empty;

    /// <summary>Optional endpoint to validate tokens (relative to ApiBaseUrl).</summary>
    public string ValidateEndpoint { get; set; } = "/health";

    /// <summary>When true, validator will attempt an HTTP call; otherwise only basic non-empty checks are performed.</summary>
    public bool UseHttpValidation { get; set; } = false;
}
