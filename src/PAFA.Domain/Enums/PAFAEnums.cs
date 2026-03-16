namespace PAFA.Domain.Enums;

public enum IngestionJobStatus
{
    Started, Processing, Completed, Failed, PartiallyCompleted, Cancelled
}

public enum IngestionFileStatus
{
    Downloaded, Validating, Valid, Invalid, Loaded, Failed
}

public enum ValidationStatus
{
    Pending, Passed, PassedWithWarnings, Failed
}

public enum FileType
{
    Xlsx, Xls, Csv, Xml
}

public enum JobTrigger
{
    Scheduler, Manual, Api, Retry
}

public enum ReportStatus
{
    Pending, Generating, Generated, Published, Archived, Failed
}

public enum ReportAudience
{
    Industry,   // 2A — anonymisé
    PAC         // 2B — non-anonymisé
}

public enum ExportFormat
{
    Csv, Excel, Pdf, PowerBiEmbedded
}