using MediatR;
using PAFA.Domain.Enums;

namespace PAFA.Extraction.Commands.Import;

public record UploadParrFilesCommand(
    string FileName,
    int PeriodYear,
    int PeriodMonth,
    string? BlobPath     = null,
    string TriggerSource = "MANUAL_API",
    Guid? ParentJobId    = null,
    int RetryCount       = 0,
    JobTrigger JobTrigger = JobTrigger.Manual
) : IRequest<UploadParrFilesResult>;

public record UploadParrFilesResult(
    bool Success,
    Guid JobId,
    Guid FileId,
    string FileName,
    int RowsRead,
    int RowsValid,
    int RowsRejected,
    string? ErrorMessage
);
