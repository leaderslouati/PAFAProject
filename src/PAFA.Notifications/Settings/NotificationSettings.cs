namespace PAFA.Notifications.Settings;

/// <summary>
/// Recipient configuration for PAFA notifications.
/// Actual dispatch is handled by Azure Service Bus — see ServiceBusSettings.
/// Bound from appsettings section "Notifications".
/// </summary>
public sealed class NotificationSettings
{
    public const string SectionName = "Notifications";

    /// <summary>Recipient list for validation-failure alerts.</summary>
    public List<string> ValidationFailureRecipients { get; set; } = [];

    /// <summary>Recipient list for ingestion-failure alerts (AC10).</summary>
    public List<string> IngestionFailureRecipients { get; set; } = [];
}
