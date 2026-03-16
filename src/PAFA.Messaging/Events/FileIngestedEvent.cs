namespace PAFA.Messaging.Events;

public class FileIngestedEvent
{
    public Guid   JobId       { get; set; }
    public Guid   FileId      { get; set; }
    public string FileName    { get; set; } = string.Empty;
    public int    PeriodYear  { get; set; }
    public int    PeriodMonth { get; set; }
    public string Status      { get; set; } = string.Empty; // "COMPLETED" | "FAILED"
    public int    RowsRead    { get; set; }
    public int    RowsValid   { get; set; }
    public int    RowsRejected { get; set; }
    public DateTime IngestedAt { get; set; } = DateTime.UtcNow;
}
