namespace PAFA.Messaging.Configuration;

/// <summary>
/// Configuration for Azure Service Bus notification dispatch.
/// Bound from appsettings section "ServiceBus".
/// </summary>
public sealed class ServiceBusSettings
{
    public const string SectionName = "ServiceBus";

    /// <summary>
    /// Full connection string or managed-identity endpoint.
    /// Example: "Endpoint=sb://pafa-bus.servicebus.windows.net/;SharedAccessKeyName=...;SharedAccessKey=..."
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Topic name for validation-failure notifications.</summary>
    public string ValidationFailureTopic { get; set; } = "pafa-validation-failure";

    /// <summary>Topic name for ingestion-failure notifications (AC10).</summary>
    public string IngestionFailureTopic { get; set; } = "pafa-ingestion-failure";

    /// <summary>Topic name for welcome / user-provisioning notifications.</summary>
    public string WelcomeTopic { get; set; } = "pafa-user-welcome";

    /// <summary>
    /// Recipients notified on validation failures.
    /// Carried inside the Service Bus message payload so the downstream
    /// consumer (notification function / Logic App) knows who to alert.
    /// </summary>
    public List<string> ValidationFailureRecipients { get; set; } = [];

    /// <summary>Recipients for ingestion-failure alerts (AC10).</summary>
    public List<string> IngestionFailureRecipients { get; set; } = [];
}
