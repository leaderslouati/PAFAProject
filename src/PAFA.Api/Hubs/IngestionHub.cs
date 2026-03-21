using Microsoft.AspNetCore.SignalR;

namespace PAFA.Api.Hubs;

/// <summary>
/// SignalR hub for real-time ingestion notifications.
/// The frontend connects and receives:
///   - "FileDownloaded"    ? file picked up from SFTP and saved to blob
///   - "ProcessingComplete" ? file processed successfully
///   - "ValidationError"    ? file failed validation (alert)
/// </summary>
public class IngestionHub : Hub
{
    private readonly ILogger<IngestionHub> _log;

    public IngestionHub(ILogger<IngestionHub> log) => _log = log;

    public override Task OnConnectedAsync()
    {
        _log.LogInformation("SignalR client connected: {ConnectionId}", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _log.LogInformation("SignalR client disconnected: {ConnectionId}", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
