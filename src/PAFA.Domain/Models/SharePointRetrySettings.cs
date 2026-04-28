namespace PAFA.Domain.Models;

/// <summary>
/// Configuration for the SharePoint retry mechanism (AC9).
/// 3 retries with exponential backoff: 5min, 15min, 30min.
/// </summary>
public sealed class SharePointRetrySettings
{
    public const string SectionName = "SharePoint:Retry";

    /// <summary>Maximum number of retry attempts before definitive failure.</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>Delay in minutes for each retry attempt (exponential backoff).</summary>
    public int[] DelayMinutes { get; set; } = [5, 15, 30];

    /// <summary>
    /// Email recipients to notify after all retries have been exhausted (AC10).
    /// </summary>
    public List<string> FailureNotificationRecipients { get; set; } = [];
}
